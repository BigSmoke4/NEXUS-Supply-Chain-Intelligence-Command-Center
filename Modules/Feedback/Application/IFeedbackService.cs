using NEXUS.Modules.Feedback.Domain;

namespace NEXUS.Modules.Feedback.Application;

public sealed record RecordDecisionOutcomeCommand(
    Guid DecisionId,
    Guid? ExecutionId,
    decimal PredictedRevenueLossUsd = 420_000m,
    decimal ActualRevenueLossUsd = 390_000m,
    decimal PredictedAdditionalCostUsd = 420_000m,
    decimal ActualAdditionalCostUsd = 408_500m,
    decimal PredictedServiceLevelPct = 98.2m,
    decimal ActualServiceLevelPct = 98.5m,
    decimal PredictedRecoveryDays = 5.0m,
    decimal ActualRecoveryDays = 4.6m,
    string RecordedBy = "Operations Manager");

public sealed record DecisionFeedbackPairDto(
    DecisionOutcome Outcome,
    DecisionQuality Quality);

public interface IFeedbackService
{
    Task<IReadOnlyList<DecisionFeedbackPairDto>> GetFeedbackHistoryAsync(int count = 25, CancellationToken cancellationToken = default);
    Task<DecisionFeedbackPairDto> RecordOutcomeAndEvaluateQualityAsync(RecordDecisionOutcomeCommand command, CancellationToken cancellationToken = default);

    DecisionQuality CalculateDecisionQualityMetrics(
        Guid decisionId,
        string decisionCode,
        Guid outcomeId,
        decimal predictedRevenueLossUsd,
        decimal actualRevenueLossUsd,
        decimal predictedCostUsd,
        decimal actualCostUsd,
        decimal predictedSlaPct,
        decimal actualSlaPct);
}
