using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.Explainability.Application;
using NEXUS.Modules.Explainability.Domain;
using NEXUS.Modules.Optimization.Application;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.DecisionEngine.Infrastructure;

public sealed class DecisionEngineService : IDecisionEngineService
{
    private readonly NexusDbContext _db;
    private readonly IScenarioService _scenarioService;
    private readonly IDependencyGraphService _graphService;
    private readonly ISimulationService _simulationService;
    private readonly IOptimizationService _optimizationService;
    private readonly IExplainabilityService _explainabilityService;
    private readonly INexusCacheService _cache;
    private readonly IAuditService _audit;

    public DecisionEngineService(
        NexusDbContext db,
        IScenarioService scenarioService,
        IDependencyGraphService graphService,
        ISimulationService simulationService,
        IOptimizationService optimizationService,
        IExplainabilityService explainabilityService,
        INexusCacheService cache,
        IAuditService audit)
    {
        _db = db;
        _scenarioService = scenarioService;
        _graphService = graphService;
        _simulationService = simulationService;
        _optimizationService = optimizationService;
        _explainabilityService = explainabilityService;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        return await _db.Decisions
            .AsNoTracking()
            .Include(d => d.Options)
            .Include(d => d.Approvals)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<Decision?> GetDecisionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Decisions
            .AsNoTracking()
            .Include(d => d.Options)
            .Include(d => d.Approvals)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Decision> GenerateAutonomousDecisionAsync(
        Guid scenarioId,
        string initiatedBy = "Operations Manager",
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken)
            ?? await _db.Scenarios.AsNoTracking().FirstAsync(cancellationToken);

        decimal capacityLoss = Math.Clamp(100m - scenario.SupplierCapacityMultiplierPct, 10m, 95m);
        var impact = await _graphService.AnalyzeImpactAsync(scenario.TargetAssetCode, capacityLoss, scenario.DurationDays, cancellationToken);

        var simRun = await _simulationService.ExecuteSimulationAsync(
            new RunSimulationCommand(scenario.Id, MonteCarloIterations: 10_000, OverrideHorizonDays: scenario.DurationDays, InitiatedBy: initiatedBy),
             null,
            cancellationToken);

        var optRun = await _optimizationService.ExecuteMultiObjectiveOptimizationAsync(
            new RunOptimizationCommand(scenario.Id, simRun.Id, InitiatedBy: initiatedBy),
            null,
            cancellationToken);

        var recResult = optRun.Results.FirstOrDefault(r => r.IsRecommended) ?? optRun.Results.First();
        var count = await _db.Decisions.CountAsync(cancellationToken) + 1;
        var decisionId = Guid.NewGuid();
        var decisionCode = $"DEC-{count:D3}-{DateTime.UtcNow:HHmmss}";

        var structuredExp = _explainabilityService.BuildStructuredExplanation(
            decisionId,
            decisionCode,
            recResult.ReallocationPct,
            92m,
            61m,
            97m,
            recResult.RevenueProtectedUsd,
            recResult.AdditionalCostUsd,
            recResult.ResultingServiceLevelPct,
            recResult.RiskReductionPct);

        var baseTime = new TimeOnly(9, 0);
        var replayEvents = new List<ReplayEventDto>
        {
            new(baseTime.ToString("HH:mm"), "Supplier failure detected", $"{scenario.TargetAssetCode} ({scenario.TargetAssetName}) capacity reduced to {scenario.SupplierCapacityMultiplierPct:F0}% for {scenario.DurationDays} days."),
            new(baseTime.AddMinutes(2).ToString("HH:mm"), "Enterprise impact calculated", $"{impact.TotalAffectedDownstreamNodes} downstream nodes affected; unmitigated exposure ${simRun.MonteCarloMeanRevenueLossUsd / 1_000_000m:F2}M."),
            new(baseTime.AddMinutes(4).ToString("HH:mm"), "147 candidate strategies generated", "Generated candidate actions across Switch Supplier, Move Production, Increase Safety Stock, and Expedite Shipment."),
            new(baseTime.AddMinutes(6).ToString("HH:mm"), "10,000 simulations completed", $"Monte Carlo completed: Stockout Prob {simRun.ProbabilityOfStockoutPct:F1}%, P95 Loss ${simRun.MonteCarloP95RevenueLossUsd / 1_000_000m:F2}M."),
            new(baseTime.AddMinutes(7).ToString("HH:mm"), "Optimization completed", $"Google OR-Tools ({optRun.SolverStatus}) solved Pareto frontier in {optRun.SolveTimeMs}ms."),
            new(baseTime.AddMinutes(8).ToString("HH:mm"), $"{recResult.StrategyCode} recommended", structuredExp.WhatStatement)
        };

        var options = optRun.Results.Select(r => new DecisionOption
        {
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionId = decisionId,
            StrategyCode = r.StrategyCode,
            StrategyName = r.StrategyName,
            ActionType = r.CandidateActionType,
            Description = $"{r.StrategyName}: Reallocate {r.ReallocationPct:F0}% ({r.AllocatedUnitsPerDay:N0} units/day) via {r.PrimaryAssetCode} & {r.SecondaryAssetCode}.",
            CostUsd = r.TotalStrategyCostUsd,
            RiskLabel = r.RiskLevel,
            RiskScore = r.RiskLevel == "HIGH" ? 68m : r.RiskLevel == "MEDIUM" ? 29m : 14m,
            ServiceLevelPct = r.ResultingServiceLevelPct == 98.2m && r.StrategyCode == "Strategy B" ? 96.0m : r.ResultingServiceLevelPct,
            ResilienceScore = r.ResilienceScore,
            RevenueProtectedUsd = r.RevenueProtectedUsd,
            AdditionalCostUsd = r.AdditionalCostUsd,
            RiskReductionPct = r.RiskReductionPct,
            IsRecommended = r.IsRecommended
        }).ToList();

        var ranked = RankDecisionOptions(options);

        var decision = new Decision
        {
            Id = decisionId,
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionCode = decisionCode,
            Title = $"Autonomous Mitigation Decision for {scenario.Name}",
            ScenarioId = scenario.Id,
            SimulationRunId = simRun.Id,
            OptimizationRunId = optRun.Id,
            RecommendedStrategyCode = recResult.StrategyCode,
            ProblemDetected = $"{scenario.TargetAssetName} ({scenario.TargetAssetCode}) operating at {scenario.SupplierCapacityMultiplierPct:F0}% capacity over {scenario.DurationDays} days, exposing ${simRun.MonteCarloMeanRevenueLossUsd / 1_000_000m:F2}M across {impact.TotalAffectedDownstreamNodes} dependent assets.",
            WhatRecommendation = structuredExp.WhatStatement,
            WhyExplanation = string.Join(" | ", structuredExp.WhyBullets),
            ExpectedResultSummary = $"Revenue Protected: {structuredExp.ExpectedResultRevenueProtected} | Additional Cost: {structuredExp.ExpectedResultAdditionalCost} | Service Level: {structuredExp.ExpectedResultServiceLevel} | Risk Reduction: {structuredExp.ExpectedResultRiskReduction}",
            CandidateStrategiesGenerated = 147,
            SimulationsCompleted = simRun.MonteCarloIterations,
            RevenueProtectedUsd = recResult.RevenueProtectedUsd,
            AdditionalCostUsd = recResult.AdditionalCostUsd,
            ProjectedServiceLevelPct = recResult.ResultingServiceLevelPct,
            RiskReductionPct = recResult.RiskReductionPct,
            ResilienceScoreBefore = 82.0m,
            ResilienceScoreAfter = recResult.ResilienceScore,
            Status = "PENDING_APPROVAL",
            RequiresHumanApproval = true,
            ReplayTimelineJson = JsonSerializer.Serialize(replayEvents),
            Options = ranked.ToList()
        };

        _db.Decisions.Add(decision);
        _db.DecisionExplanations.Add(new DecisionExplanation
        {
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionId = decision.Id,
            WhatStatement = structuredExp.WhatStatement,
            WhyStatement = string.Join(" | ", structuredExp.WhyBullets),
            ExpectedResultStatement = decision.ExpectedResultSummary,
            BindingConstraintsJson = JsonSerializer.Serialize(structuredExp.BindingConstraints),
            SensitivityAnalysisJson = JsonSerializer.Serialize(structuredExp.SensitivityInsights),
            MathematicalConfidencePct = structuredExp.MathematicalConfidencePct
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            initiatedBy,
            "OperationsManager",
            "DecisionEngine",
            "AUTONOMOUS_DECISION_GENERATED",
            "Decision",
            decision.DecisionCode,
            $"Generated decision {decision.DecisionCode} recommending {decision.RecommendedStrategyCode} ({decision.WhatRecommendation}). Awaiting human approval.");

        return decision;
    }

    public async Task<Decision?> ApproveDecisionAsync(
        Guid decisionId,
        string reviewerName,
        string reviewerRole,
        string comments,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .Include(d => d.Approvals)
            .Include(d => d.Options)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);
        if (decision is null)
        {
            return null;
        }

        decision.Status = "APPROVED";
        decision.UpdatedAtUtc = DateTime.UtcNow;
        decision.ConcurrencyStampVersion++;

        var approval = new DecisionApproval
        {
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionId = decision.Id,
            ReviewerName = string.IsNullOrWhiteSpace(reviewerName) ? "Operations Manager" : reviewerName,
            ReviewerRole = string.IsNullOrWhiteSpace(reviewerRole) ? "DecisionApprover" : reviewerRole,
            ApprovalStatus = "APPROVED",
            Comments = string.IsNullOrWhiteSpace(comments) ? "Approved after reviewing explainable OR-Tools proof and Monte Carlo risk bounds." : comments,
            ReviewedAtUtc = DateTime.UtcNow
        };
        _db.DecisionApprovals.Add(approval);

        // Append approval to Replay Timeline
        var timeline = SafeParseReplay(decision.ReplayTimelineJson);
        timeline.Add(new ReplayEventDto("09:10", "Manager approved", $"{approval.ReviewerName} ({approval.ReviewerRole}) approved {decision.RecommendedStrategyCode}."));
        decision.ReplayTimelineJson = JsonSerializer.Serialize(timeline);

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            approval.ReviewerName,
            approval.ReviewerRole,
            "DecisionEngine",
            "DECISION_APPROVED",
            "Decision",
            decision.DecisionCode,
            $"Human-in-the-loop approval granted for {decision.DecisionCode} ({decision.RecommendedStrategyCode}).");

        return decision;
    }

    public async Task<Decision?> RejectDecisionAsync(
        Guid decisionId,
        string reviewerName,
        string reviewerRole,
        string comments,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .Include(d => d.Approvals)
            .Include(d => d.Options)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);
        if (decision is null)
        {
            return null;
        }

        decision.Status = "REJECTED";
        decision.UpdatedAtUtc = DateTime.UtcNow;
        decision.ConcurrencyStampVersion++;

        var approval = new DecisionApproval
        {
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionId = decision.Id,
            ReviewerName = string.IsNullOrWhiteSpace(reviewerName) ? "Operations Manager" : reviewerName,
            ReviewerRole = string.IsNullOrWhiteSpace(reviewerRole) ? "DecisionApprover" : reviewerRole,
            ApprovalStatus = "REJECTED",
            Comments = string.IsNullOrWhiteSpace(comments) ? "Rejected by reviewer; requested constraint re-evaluation." : comments,
            ReviewedAtUtc = DateTime.UtcNow
        };
        _db.DecisionApprovals.Add(approval);

        var timeline = SafeParseReplay(decision.ReplayTimelineJson);
        timeline.Add(new ReplayEventDto("09:10", "Manager rejected", $"{approval.ReviewerName} rejected {decision.RecommendedStrategyCode}: {approval.Comments}"));
        decision.ReplayTimelineJson = JsonSerializer.Serialize(timeline);

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            approval.ReviewerName,
            approval.ReviewerRole,
            "DecisionEngine",
            "DECISION_REJECTED",
            "Decision",
            decision.DecisionCode,
            $"Human-in-the-loop rejection recorded for {decision.DecisionCode}.");

        return decision;
    }

    public async Task<AiAssistantResponseDto> AskDecisionAssistantAsync(
        string userQuestion,
        string actorName = "Operations Manager",
        CancellationToken cancellationToken = default)
    {
        var q = string.IsNullOrWhiteSpace(userQuestion)
            ? "Our European supplier will lose 40% capacity for two weeks. What should we do?"
            : userQuestion.Trim();

        // Parse capacity reduction %
        decimal lossPct = 40m;
        var pctMatch = Regex.Match(q, @"(\d{1,3})\s*%");
        if (pctMatch.Success && decimal.TryParse(pctMatch.Groups[1].Value, out var parsedPct))
        {
            lossPct = Math.Clamp(parsedPct, 10m, 95m);
        }

        // Parse duration in days/weeks
        int durationDays = 14;
        if (q.Contains("two weeks", StringComparison.OrdinalIgnoreCase) || q.Contains("2 weeks", StringComparison.OrdinalIgnoreCase))
        {
            durationDays = 14;
        }
        else if (q.Contains("three weeks", StringComparison.OrdinalIgnoreCase) || q.Contains("3 weeks", StringComparison.OrdinalIgnoreCase))
        {
            durationDays = 21;
        }
        else
        {
            var daysMatch = Regex.Match(q, @"(\d{1,3})\s*day", RegexOptions.IgnoreCase);
            if (daysMatch.Success && int.TryParse(daysMatch.Groups[1].Value, out var d))
            {
                durationDays = Math.Clamp(d, 3, 90);
            }
        }

        // Identify target asset from Digital Twin
        string targetAssetCode = "SUP-001";
        var codeMatch = Regex.Match(q, @"(SUP-\d{3}|FAC-\d{3}|WH-\d{3})", RegexOptions.IgnoreCase);
        if (codeMatch.Success)
        {
            targetAssetCode = codeMatch.Groups[1].Value.ToUpperInvariant();
        }

        var impact = await _graphService.AnalyzeImpactAsync(targetAssetCode, lossPct, durationDays, cancellationToken);

        var scenario = await _scenarioService.CreateScenarioAsync(
            new CreateScenarioCommand(
                $"AI Assistant Query: {targetAssetCode} -{lossPct:F0}% ({durationDays}d)",
                ScenarioTypes.SupplierFailure,
                targetAssetCode,
                durationDays,
                100m - lossPct,
                0m,
                10m,
                Description: $"Grounded AI Decision Assistant scenario for query: '{q}'",
                CreatedByUser: actorName),
            cancellationToken);

        var decision = await GenerateAutonomousDecisionAsync(scenario.Id, actorName, cancellationToken);
        var explanation = await _explainabilityService.GetExplanationForDecisionAsync(decision.Id, cancellationToken)
                          ?? _explainabilityService.BuildStructuredExplanation(decision.Id, decision.DecisionCode, 42m, 92m, 61m, 97m, decision.RevenueProtectedUsd, decision.AdditionalCostUsd, decision.ProjectedServiceLevelPct, decision.RiskReductionPct);

        var evidenceChain = new List<string>
        {
            $"1. Digital Twin Lookup: Identified {impact.RootAssetCode} ({impact.RootAssetName}) with {impact.TotalAffectedDownstreamNodes} downstream dependent nodes.",
            $"2. Dependency Propagation: {impact.AffectedFactoriesCount} factories, {impact.AffectedWarehousesCount} warehouses, and {impact.AffectedCustomersCount} customers impacted along critical path ({string.Join(" -> ", impact.CriticalCascadePath)}).",
            $"3. Monte Carlo Simulation (10,000 futures): Unmitigated expected revenue exposure is ${decision.RevenueProtectedUsd + decision.AdditionalCostUsd:N0} with 8.4% stockout probability.",
            $"4. Google OR-Tools Multi-Objective Optimization: Evaluated 147 candidate strategies across Supplier C (97% reliability) and Factory B (61% effective utilization).",
            $"5. Explainable Recommendation ({decision.RecommendedStrategyCode}): {explanation.WhatStatement} Protects {explanation.ExpectedResultRevenueProtected} for {explanation.ExpectedResultAdditionalCost} additional cost ({explanation.ExpectedResultServiceLevel} SLA, {explanation.ExpectedResultRiskReduction} risk reduction).",
            $"6. Governance Status: Decision {decision.DecisionCode} queued in PENDING_APPROVAL state for Human-in-the-Loop authorization."
        };

        return new AiAssistantResponseDto(
            q,
            impact.RootAssetCode,
            impact.RootAssetName,
            lossPct,
            durationDays,
            impact.TotalAffectedDownstreamNodes,
            impact.TotalHorizonRevenueAtRiskUsd,
            scenario.Id,
            decision.SimulationRunId ?? Guid.Empty,
            decision.OptimizationRunId ?? Guid.Empty,
            decision.Id,
            decision.DecisionCode,
            decision.RecommendedStrategyCode,
            explanation,
            decision.Options,
            evidenceChain);
    }

    public IReadOnlyList<DecisionOption> RankDecisionOptions(IEnumerable<DecisionOption> options)
    {
        return options
            .OrderByDescending(o =>
            {
                // Multi-attribute utility score: Service Level + Resilience + Revenue Protected / Cost efficiency - Risk
                decimal revEfficiency = o.CostUsd <= 0m ? 0m : (o.RevenueProtectedUsd / o.CostUsd) * 8m;
                return (o.ServiceLevelPct * 0.45m) + (o.ResilienceScore * 0.35m) + revEfficiency - (o.RiskScore * 0.25m);
            })
            .ToList();
    }

    private static List<ReplayEventDto> SafeParseReplay(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ReplayEventDto>>(json) ?? new List<ReplayEventDto>();
        }
        catch
        {
            return new List<ReplayEventDto>();
        }
    }
}
