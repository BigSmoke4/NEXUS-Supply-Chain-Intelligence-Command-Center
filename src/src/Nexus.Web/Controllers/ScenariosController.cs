using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.AI;
using Nexus.Web.Modules.Audit;
using Nexus.Web.Modules.BackgroundJobs;
using Nexus.Web.Modules.NaturalLanguage;
using Nexus.Web.Modules.Observability;
using Nexus.Web.Modules.Validation;
using Nexus.Web.Security;

namespace Nexus.Web.Controllers;

[Authorize(Policy = NexusPermissions.SimulationRead)]
public class ScenariosController : Controller
{
    private readonly NexusDbContext _db;
    private readonly ISimulationJobQueue _jobQueue;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IAuditService _audit;
    private readonly IValidator<RunScenarioRequest> _validator;
    private readonly INaturalLanguageScenarioParser _nlParser;
    private readonly NexusMetrics _metrics;
    private readonly ICurrentTenant _tenant;

    public ScenariosController(
        NexusDbContext db, ISimulationJobQueue jobQueue,
        IAgentOrchestrator orchestrator, IAuditService audit,
        IValidator<RunScenarioRequest> validator, INaturalLanguageScenarioParser nlParser, NexusMetrics metrics, ICurrentTenant tenant)
    {
        _db = db;
        _jobQueue = jobQueue;
        _orchestrator = orchestrator;
        _audit = audit;
        _validator = validator;
        _nlParser = nlParser;
        _metrics = metrics;
        _tenant = tenant;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = NexusPermissions.SimulationCreate)]
    public async Task<IActionResult> ParseNaturalLanguage(string text, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
        var candidates = await _db.SupplyNodes
            .Where(n => n.OrganizationId == orgId
                && (n.Type == NodeType.Supplier || n.Type == NodeType.Factory || n.Type == NodeType.Warehouse))
            .Select(n => new { n.Id, n.Name, n.Type })
            .ToListAsync(ct);

        var result = _nlParser.Parse(text, candidates.Select(c => (c.Id, c.Name, c.Type)).ToList());

        return Json(result.Succeeded
            ? new
            {
                succeeded = true,
                nodeId = result.Scenario!.NodeId,
                nodeName = result.Scenario.NodeName,
                type = result.Scenario.Type.ToString(),
                durationDays = result.Scenario.DurationDays,
                severityPercent = result.Scenario.SeverityPercent
            }
            : new { succeeded = false, clarifications = result.ClarificationsNeeded });
    }

    [HttpGet]
    public async Task<IActionResult> Builder(CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
        var nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == orgId
                && (n.Type == NodeType.Supplier || n.Type == NodeType.Factory || n.Type == NodeType.Warehouse))
                .OrderBy(n => n.Name).ToListAsync(ct);

        ViewBag.Nodes = nodes;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = NexusPermissions.SimulationCreate)]
    public async Task<IActionResult> Run(Guid nodeId, DisruptionType type, int durationDays, double severityPercent, string? name, CancellationToken ct)
    {
        var request = new RunScenarioRequest(nodeId, type, durationDays, severityPercent, name);
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(string.Empty, error.ErrorMessage);

            var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
            ViewBag.Nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == orgId
                    && (n.Type == NodeType.Supplier || n.Type == NodeType.Factory || n.Type == NodeType.Warehouse))
                .OrderBy(n => n.Name).ToListAsync(ct);
            return View("Builder");
        }

        var org2Id = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
        var start = DateTime.UtcNow.Date.AddDays(1);

        var scenario = new Scenario
        {
            OrganizationId = org2Id,
            Name = string.IsNullOrWhiteSpace(name) ? $"{type} - {durationDays}d" : name,
            Disruptions = new List<Disruption>
            {
                new()
                {
                    OrganizationId = org2Id,
                    Type = type,
                    AffectedNodeId = nodeId,
                    CapacityReductionPercent = severityPercent,
                    StartDateUtc = start,
                    EndDateUtc = start.AddDays(durationDays),
                    Assumptions = "Demand remains within historical range."
                }
            }
        };

        _db.Scenarios.Add(scenario);
        await _db.SaveChangesAsync(ct);

        // §46: the simulation runs off the request thread. Enqueue and
        // return immediately - SimulationBackgroundWorker computes the
        // result and pushes SimulationStarted/SimulationCompleted over
        // SignalR to the "scenario-{id}" group; the Results page joins that
        // group and refreshes itself when the result lands (nexus.signalr.js).
        await _jobQueue.EnqueueAsync(new SimulationJob(org2Id, scenario.Id), ct);

        await _audit.RecordAsync(
            org2Id, User?.Identity?.Name, "Scenario.Queued", nameof(Scenario), scenario.Id, ct: ct);

        return RedirectToAction(nameof(Results), new { id = scenario.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Results(Guid id, CancellationToken ct)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.Disruptions)
            .Include(s => s.LastResult)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (scenario is null) return NotFound();

        // The graph + cascade animation (§56) need the full node/edge set to
        // render against, the same way CommandCenterController supplies it.
        ViewBag.Nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == scenario.OrganizationId).ToListAsync(ct);
        ViewBag.Edges = await _db.SupplyEdges.Where(e => e.OrganizationId == scenario.OrganizationId).ToListAsync(ct);

        return View(scenario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = NexusPermissions.AIExecute)]
    public async Task<IActionResult> Explain(Guid id, string? question, CancellationToken ct)
    {
        var scenario = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (scenario is null) return NotFound();

        // Free-text question from the user (§16 - this is genuinely
        // arbitrary input, unlike the earlier hardcoded canned question).
        // Length-capped defensively; content is otherwise unrestricted
        // because the architecture (fixed system prompt, evidence-as-data
        // framing) is what has to hold up against adversarial input here,
        // not input filtering - see PromptInjectionDefenseTests.
        var effectiveQuestion = string.IsNullOrWhiteSpace(question)
            ? "What is the impact of this disruption and how should we respond?"
            : question.Length > 500 ? question[..500] : question;

        var investigation = await _orchestrator.InvestigateScenarioAsync(scenario.OrganizationId, id, effectiveQuestion, ct);
        return PartialView("_AiExplanation", investigation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = NexusPermissions.MitigationApprove)]
    public async Task<IActionResult> ApproveStrategy(Guid strategyId, Guid scenarioId, CancellationToken ct)
    {
        var strategy = await _db.MitigationStrategies.FindAsync(new object[] { strategyId }, ct);
        if (strategy is null) return NotFound();

        var scenario = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == scenarioId, ct);
        if (scenario is null) return NotFound();

        var before = strategy.ApprovalStatus.ToString();

        // Human-in-the-loop gate (§19): the AI/optimization engine only ever
        // recommends; only an explicit authorized action changes ApprovalStatus.
        strategy.ApprovalStatus = ApprovalStatus.Approved;
        await _db.SaveChangesAsync(ct);
        _metrics.MitigationStrategiesApproved.Add(1);

        // Every approval of an AI-sourced recommendation is written to the
        // immutable audit trail with the evidence that backed it (§38).
        await _audit.RecordAsync(
            scenario.OrganizationId, User?.Identity?.Name, "MitigationStrategy.Approve",
            nameof(MitigationStrategy), strategy.Id,
            beforeState: before, afterState: strategy.ApprovalStatus.ToString(),
            aiAgent: "Optimization Agent", aiConfidence: strategy.ConfidencePercent,
            aiEvidenceCount: strategy.EvidenceCount, ct: ct);

        return RedirectToAction(nameof(Results), new { id = scenarioId });
    }
}
