using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.Execution.Application;
using NEXUS.Modules.Explainability.Application;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;

namespace NEXUS.Modules.DecisionEngine.Presentation;

public sealed record DecisionEngineViewModel(
    Decision ActiveDecision,
    ExplainableRecommendationDto Explanation,
    IReadOnlyList<ReplayEventDto> ReplayTimeline,
    IReadOnlyList<Decision> AllDecisions,
    IReadOnlyList<Scenario> Scenarios,
    AiAssistantResponseDto? AssistantResponse = null);

public sealed class DecisionEngineController : Controller
{
    private readonly IDecisionEngineService _decisionService;
    private readonly IExplainabilityService _explainabilityService;
    private readonly IScenarioService _scenarioService;
    private readonly IExecutionService _executionService;

    public DecisionEngineController(
        IDecisionEngineService decisionService,
        IExplainabilityService explainabilityService,
        IScenarioService scenarioService,
        IExecutionService executionService)
    {
        _decisionService = decisionService;
        _explainabilityService = explainabilityService;
        _scenarioService = scenarioService;
        _executionService = executionService;
    }

    [HttpGet("/DecisionEngine")]
    [HttpGet("/DecisionEngine/Details/{id:guid}")]
    public async Task<IActionResult> Index(Guid? id = null, CancellationToken cancellationToken = default)
    {
        var all = await _decisionService.GetDecisionsAsync(25, cancellationToken);
        var scenarios = await _scenarioService.GetScenariosAsync(cancellationToken);
        var active = id.HasValue
            ? await _decisionService.GetDecisionByIdAsync(id.Value, cancellationToken) ?? all.First()
            : all.First();

        var exp = await _explainabilityService.GetExplanationForDecisionAsync(active.Id, cancellationToken)
                  ?? _explainabilityService.BuildStructuredExplanation(active.Id, active.DecisionCode, 42m, 92m, 61m, 97m, active.RevenueProtectedUsd, active.AdditionalCostUsd, active.ProjectedServiceLevelPct, active.RiskReductionPct);

        var replay = ParseReplay(active.ReplayTimelineJson);

        return View("~/Views/DecisionEngine/Index.cshtml", new DecisionEngineViewModel(active, exp, replay, all, scenarios));
    }

    [HttpPost("/DecisionEngine/Generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(Guid scenarioId, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var decision = await _decisionService.GenerateAutonomousDecisionAsync(scenarioId, actor, cancellationToken);
        return RedirectToAction(nameof(Index), new { id = decision.Id });
    }

    [HttpPost("/DecisionEngine/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid decisionId, string? comments, CancellationToken cancellationToken)
    {
        var reviewer = User.Identity?.Name ?? "Marcus Vance (VP Global Operations)";
        await _decisionService.ApproveDecisionAsync(decisionId, reviewer, "DecisionApprover", comments ?? "Approved Strategy B via Human-in-the-Loop review.", cancellationToken);
        return RedirectToAction(nameof(Index), new { id = decisionId });
    }

    [HttpPost("/DecisionEngine/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid decisionId, string? comments, CancellationToken cancellationToken)
    {
        var reviewer = User.Identity?.Name ?? "Marcus Vance (VP Global Operations)";
        await _decisionService.RejectDecisionAsync(decisionId, reviewer, "DecisionApprover", comments ?? "Rejected by reviewer.", cancellationToken);
        return RedirectToAction(nameof(Index), new { id = decisionId });
    }

    [HttpPost("/DecisionEngine/Execute")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Execute(Guid decisionId, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        await _executionService.ExecuteApprovedDecisionAsync(decisionId, actor, cancellationToken);
        return RedirectToAction("Index", "Execution");
    }

    private static IReadOnlyList<ReplayEventDto> ParseReplay(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ReplayEventDto>>(json) ?? new List<ReplayEventDto>();
        }
        catch
        {
            return Array.Empty<ReplayEventDto>();
        }
    }
}
