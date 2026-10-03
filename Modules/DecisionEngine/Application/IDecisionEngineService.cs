using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.Explainability.Application;

namespace NEXUS.Modules.DecisionEngine.Application;

public sealed record ReplayEventDto(
    string Time,
    string Event,
    string Detail);

public sealed record AiAssistantResponseDto(
    string UserQuestion,
    string IdentifiedTargetAssetCode,
    string IdentifiedTargetAssetName,
    decimal ParsedCapacityLossPct,
    int ParsedDurationDays,
    int AffectedDownstreamNodesCount,
    decimal UnmitigatedRevenueAtRiskUsd,
    Guid GeneratedScenarioId,
    Guid SimulationRunId,
    Guid OptimizationRunId,
    Guid DecisionId,
    string DecisionCode,
    string RecommendedStrategyCode,
    ExplainableRecommendationDto Explanation,
    IReadOnlyList<DecisionOption> FrontierStrategies,
    IReadOnlyList<string> GroundedEvidenceChain);

public interface IDecisionEngineService
{
    Task<IReadOnlyList<Decision>> GetDecisionsAsync(int count = 25, CancellationToken cancellationToken = default);
    Task<Decision?> GetDecisionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Decision> GenerateAutonomousDecisionAsync(
        Guid scenarioId,
        string initiatedBy = "Operations Manager",
        CancellationToken cancellationToken = default);

    Task<Decision?> ApproveDecisionAsync(
        Guid decisionId,
        string reviewerName,
        string reviewerRole,
        string comments,
        CancellationToken cancellationToken = default);

    Task<Decision?> RejectDecisionAsync(
        Guid decisionId,
        string reviewerName,
        string reviewerRole,
        string comments,
        CancellationToken cancellationToken = default);

    Task<AiAssistantResponseDto> AskDecisionAssistantAsync(
        string userQuestion,
        string actorName = "Operations Manager",
        CancellationToken cancellationToken = default);

    IReadOnlyList<DecisionOption> RankDecisionOptions(IEnumerable<DecisionOption> options);
}
