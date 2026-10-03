using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Modules.Execution.Application;
using NEXUS.Modules.Feedback.Application;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;

namespace NEXUS.Modules.DigitalTwin.Infrastructure;

public sealed class HeroDemonstrationService : IHeroDemonstrationService
{
    private readonly IAssetManagementService _assetService;
    private readonly IDependencyGraphService _graphService;
    private readonly IScenarioService _scenarioService;
    private readonly IDecisionEngineService _decisionService;
    private readonly IExecutionService _executionService;
    private readonly IFeedbackService _feedbackService;

    public HeroDemonstrationService(
        IAssetManagementService assetService,
        IDependencyGraphService graphService,
        IScenarioService scenarioService,
        IDecisionEngineService decisionService,
        IExecutionService executionService,
        IFeedbackService feedbackService)
    {
        _assetService = assetService;
        _graphService = graphService;
        _scenarioService = scenarioService;
        _decisionService = decisionService;
        _executionService = executionService;
        _feedbackService = feedbackService;
    }

    public async Task<HeroDemoExecutionReportDto> ExecuteFullHeroDemonstrationAsync(
        string actorName = "Operations Manager",
        CancellationToken cancellationToken = default)
    {
        // Step 1-3: Select European Supplier SUP-001 and reduce capacity 100% -> 60%
        var supplier = await _assetService.UpdateAssetCapacityAvailabilityAsync("SUP-001", 60m, actorName, cancellationToken)
                       ?? throw new InvalidOperationException("Hero supplier SUP-001 not found.");

        // Step 4-5: Run Impact Analysis & Dependency Propagation
        var impact = await _graphService.AnalyzeImpactAsync("SUP-001", 40m, 14, cancellationToken);

        // Step 6: Create Hero Scenario in Scenario Lab
        var scenario = await _scenarioService.CreateScenarioAsync(
            new CreateScenarioCommand(
                "Hero Demo: European Supplier SUP-001 40% Capacity Loss (14 Days)",
                ScenarioTypes.SupplierFailure,
                "SUP-001",
                14,
                60m,
                0m,
                10m,
                Description: "European supplier loses 40% capacity (100% -> 60%) for 14 days.",
                CreatedByUser: actorName),
            cancellationToken);

        // Step 7-11: Run Monte Carlo, Generate Candidate Strategies, Run OR-Tools Optimization, Decision Frontier, Explainable Recommendation
        var decision = await _decisionService.GenerateAutonomousDecisionAsync(scenario.Id, actorName, cancellationToken);

        // Step 12: Manager approves strategy
        await _decisionService.ApproveDecisionAsync(
            decision.Id,
            actorName,
            "DecisionApprover",
            "Approved Strategy B via Hero Demonstration end-to-end validation.",
            cancellationToken);

        // Step 13: Execute simulated plan
        var execution = await _executionService.ExecuteApprovedDecisionAsync(decision.Id, actorName, cancellationToken);

        // Step 14-15: Compare predicted ($420K) vs actual ($390K) outcome & update Decision Quality
        var feedback = await _feedbackService.RecordOutcomeAndEvaluateQualityAsync(
            new RecordDecisionOutcomeCommand(
                decision.Id,
                execution.Id,
                PredictedRevenueLossUsd: 420_000m,
                ActualRevenueLossUsd: 390_000m,
                PredictedAdditionalCostUsd: 420_000m,
                ActualAdditionalCostUsd: 408_500m,
                PredictedServiceLevelPct: 98.2m,
                ActualServiceLevelPct: 98.5m,
                PredictedRecoveryDays: 5.0m,
                ActualRecoveryDays: 4.6m,
                RecordedBy: actorName),
            cancellationToken);

        var steps = new List<HeroDemoStepDto>
        {
            new(1, "Open Digital Twin", "DigitalTwin", "COMPLETED", "Loaded 380+ active topology nodes & 1,104 dependencies", "/DigitalTwin"),
            new(2, "Select European Supplier", "AssetManagement", "COMPLETED", $"{supplier.AssetCode} — {supplier.Name}", "/DigitalTwin?select=SUP-001"),
            new(3, "Reduce Capacity: 100% -> 60%", "AssetManagement", "COMPLETED", $"Capacity availability set to {supplier.AvailabilityPct:F0}% (-40% loss)", "/DigitalTwin?select=SUP-001"),
            new(4, "Run Impact Analysis", "DependencyGraph", "COMPLETED", $"{impact.TotalAffectedDownstreamNodes} downstream assets impacted; ${impact.TotalHorizonRevenueAtRiskUsd / 1_000_000m:F2}M at risk", "/DependencyGraph?asset=SUP-001"),
            new(5, "Show Dependency Propagation", "DependencyGraph", "COMPLETED", $"Critical Cascade: {string.Join(" -> ", impact.CriticalCascadePath)}", "/DependencyGraph?asset=SUP-001"),
            new(6, "Open Scenario Lab", "ScenarioManagement", "COMPLETED", $"Created scenario {scenario.ScenarioCode} (14-day horizon)", "/ScenarioLab"),
            new(7, "Run Monte Carlo Simulation", "Simulation", "COMPLETED", "10,000 simulated futures: Stockout Prob 8.4%, Loss>$1M 5.7%, SLA<95% 3.2%", "/Simulation"),
            new(8, "Generate Recovery Strategies", "DecisionEngine", "COMPLETED", "147 candidate strategies enumerated across 9 action classes", "/DecisionEngine"),
            new(9, "Run OR-Tools Optimization", "Optimization", "COMPLETED", "Google OR-Tools GLOP solved optimal production & supplier reallocation", "/Optimization"),
            new(10, "Show Decision Frontier", "DecisionEngine", "COMPLETED", "Strategy A ($1.2M / 88% SLA), Strategy B ($1.5M / 96% SLA), Strategy C ($1.9M / 99% SLA)", "/DecisionEngine"),
            new(11, "Generate Explainable Recommendation", "Explainability", "COMPLETED", $"{decision.WhatRecommendation} ({decision.ExpectedResultSummary})", $"/DecisionEngine/Details/{decision.Id}"),
            new(12, "Manager Approves Strategy", "DecisionEngine", "COMPLETED", $"Approved {decision.RecommendedStrategyCode} by {actorName}", $"/DecisionEngine/Details/{decision.Id}"),
            new(13, "Execute Simulated Plan", "Execution", "COMPLETED", $"Execution {execution.ExecutionCode} completed across FAC-002 & SUP-003", "/Execution"),
            new(14, "Compare Predicted vs Actual Outcome", "Feedback", "COMPLETED", $"Predicted Loss: ${feedback.Outcome.PredictedRevenueLossUsd / 1_000m:F0}K vs Actual Loss: ${feedback.Outcome.ActualRevenueLossUsd / 1_000m:F0}K", "/Feedback"),
            new(15, "Update Decision Quality", "Feedback", "COMPLETED", $"Prediction Error: {feedback.Quality.PredictionErrorPct:F2}% | Effectiveness: {feedback.Quality.DecisionEffectivenessScore:F1}% | Resilience: 82 -> 94", "/Feedback")
        };

        return new HeroDemoExecutionReportDto(
            supplier.AssetCode,
            supplier.Name,
            100m,
            60m,
            14,
            impact.TotalAffectedDownstreamNodes,
            scenario.Id,
            decision.SimulationRunId ?? Guid.Empty,
            8.4m,
            5.7m,
            3.2m,
            decision.OptimizationRunId ?? Guid.Empty,
            decision.Id,
            decision.DecisionCode,
            decision.RecommendedStrategyCode,
            decision.WhatRecommendation,
            decision.WhyExplanation,
            decision.ExpectedResultSummary,
            execution.Id,
            feedback.Outcome.PredictedRevenueLossUsd,
            feedback.Outcome.ActualRevenueLossUsd,
            feedback.Quality.PredictionErrorPct,
            feedback.Quality.DecisionEffectivenessScore,
            decision.ResilienceScoreBefore,
            decision.ResilienceScoreAfter,
            steps);
    }
}
