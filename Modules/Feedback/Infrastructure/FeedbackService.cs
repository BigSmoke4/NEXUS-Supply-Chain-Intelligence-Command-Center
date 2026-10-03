using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Feedback.Application;
using NEXUS.Modules.Feedback.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.Feedback.Infrastructure;

public sealed class FeedbackService : IFeedbackService
{
    private readonly NexusDbContext _db;
    private readonly IAuditService _audit;

    public FeedbackService(NexusDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DecisionFeedbackPairDto>> GetFeedbackHistoryAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        var outcomes = await _db.DecisionOutcomes
            .AsNoTracking()
            .OrderByDescending(o => o.MeasuredAtUtc)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);

        var qualities = await _db.DecisionQualities
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var qualityByOutcome = qualities.ToDictionary(q => q.DecisionOutcomeId);
        var list = new List<DecisionFeedbackPairDto>();

        foreach (var o in outcomes)
        {
            if (!qualityByOutcome.TryGetValue(o.Id, out var q))
            {
                q = CalculateDecisionQualityMetrics(
                    o.DecisionId,
                    o.DecisionCode,
                    o.Id,
                    o.PredictedRevenueLossUsd,
                    o.ActualRevenueLossUsd,
                    o.PredictedAdditionalCostUsd,
                    o.ActualAdditionalCostUsd,
                    o.PredictedServiceLevelPct,
                    o.ActualServiceLevelPct);
            }
            list.Add(new DecisionFeedbackPairDto(o, q));
        }

        return list;
    }

    public async Task<DecisionFeedbackPairDto> RecordOutcomeAndEvaluateQualityAsync(
        RecordDecisionOutcomeCommand command,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == command.DecisionId, cancellationToken)
            ?? await _db.Decisions.AsNoTracking().FirstAsync(cancellationToken);

        var outcome = new DecisionOutcome
        {
            Id = Guid.NewGuid(),
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionId = decision.Id,
            DecisionCode = decision.DecisionCode,
            ExecutionId = command.ExecutionId,
            PredictedRevenueLossUsd = command.PredictedRevenueLossUsd,
            ActualRevenueLossUsd = command.ActualRevenueLossUsd,
            PredictedAdditionalCostUsd = command.PredictedAdditionalCostUsd,
            ActualAdditionalCostUsd = command.ActualAdditionalCostUsd,
            PredictedServiceLevelPct = command.PredictedServiceLevelPct,
            ActualServiceLevelPct = command.ActualServiceLevelPct,
            PredictedRecoveryDays = command.PredictedRecoveryDays,
            ActualRecoveryDays = command.ActualRecoveryDays,
            MeasuredAtUtc = DateTime.UtcNow
        };

        var quality = CalculateDecisionQualityMetrics(
            decision.Id,
            decision.DecisionCode,
            outcome.Id,
            command.PredictedRevenueLossUsd,
            command.ActualRevenueLossUsd,
            command.PredictedAdditionalCostUsd,
            command.ActualAdditionalCostUsd,
            command.PredictedServiceLevelPct,
            command.ActualServiceLevelPct);

        _db.DecisionOutcomes.Add(outcome);
        _db.DecisionQualities.Add(quality);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            command.RecordedBy,
            "OperationsManager",
            "Feedback",
            "DECISION_QUALITY_EVALUATED",
            "DecisionQuality",
            decision.DecisionCode,
            $"Compared predicted loss (${command.PredictedRevenueLossUsd:N0}) vs actual (${command.ActualRevenueLossUsd:N0}). Prediction Error: {quality.PredictionErrorPct:F2}%, Effectiveness: {quality.DecisionEffectivenessScore:F1}%.");

        return new DecisionFeedbackPairDto(outcome, quality);
    }

    public DecisionQuality CalculateDecisionQualityMetrics(
        Guid decisionId,
        string decisionCode,
        Guid outcomeId,
        decimal predictedRevenueLossUsd,
        decimal actualRevenueLossUsd,
        decimal predictedCostUsd,
        decimal actualCostUsd,
        decimal predictedSlaPct,
        decimal actualSlaPct)
    {
        decimal denom = Math.Max(1m, predictedRevenueLossUsd);
        decimal predErrorPct = Math.Round(Math.Abs(predictedRevenueLossUsd - actualRevenueLossUsd) / denom * 100m, 2);

        decimal costVarUsd = Math.Round(actualCostUsd - predictedCostUsd, 2);
        decimal costVarPct = Math.Round(costVarUsd / Math.Max(1m, predictedCostUsd) * 100m, 2);
        decimal slaVarPct = Math.Round(actualSlaPct - predictedSlaPct, 2);

        decimal effectiveness = Math.Clamp(
            Math.Round(100m - (predErrorPct * 0.45m) - Math.Max(0m, costVarPct * 0.30m) + (slaVarPct * 2.0m), 1),
            0m,
            100m);

        decimal calibration = Math.Clamp(
            Math.Round(actualRevenueLossUsd / denom, 4),
            0.75m,
            1.25m);

        return new DecisionQuality
        {
            Id = Guid.NewGuid(),
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            DecisionId = decisionId,
            DecisionCode = decisionCode,
            DecisionOutcomeId = outcomeId,
            PredictionErrorPct = predErrorPct,
            DecisionEffectivenessScore = effectiveness,
            CostVarianceUsd = costVarUsd,
            CostVariancePct = costVarPct,
            ServiceLevelVariancePct = slaVarPct,
            CalibrationWeightMultiplier = calibration,
            FeedbackSummary = $"Predicted Revenue Loss ${predictedRevenueLossUsd:N0} vs Actual ${actualRevenueLossUsd:N0} (Error: {predErrorPct:F2}%, Cost Variance: ${costVarUsd:N0}, SLA Variance: {slaVarPct:+0.00;-0.00;0.00}%).",
            EvaluatedAtUtc = DateTime.UtcNow
        };
    }
}
