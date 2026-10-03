using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Feedback.Domain;

public sealed class DecisionOutcome : EntityBase
{
    public Guid DecisionId { get; set; }

    [MaxLength(64)]
    public string DecisionCode { get; set; } = string.Empty;

    public Guid? ExecutionId { get; set; }

    public decimal PredictedRevenueLossUsd { get; set; }

    public decimal ActualRevenueLossUsd { get; set; }

    public decimal PredictedAdditionalCostUsd { get; set; }

    public decimal ActualAdditionalCostUsd { get; set; }

    public decimal PredictedServiceLevelPct { get; set; }

    public decimal ActualServiceLevelPct { get; set; }

    public decimal PredictedRecoveryDays { get; set; }

    public decimal ActualRecoveryDays { get; set; }

    public DateTime MeasuredAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DecisionQuality : EntityBase
{
    public Guid DecisionId { get; set; }

    [MaxLength(64)]
    public string DecisionCode { get; set; } = string.Empty;

    public Guid DecisionOutcomeId { get; set; }

    public decimal PredictionErrorPct { get; set; }

    public decimal DecisionEffectivenessScore { get; set; }

    public decimal CostVarianceUsd { get; set; }

    public decimal CostVariancePct { get; set; }

    public decimal ServiceLevelVariancePct { get; set; }

    public decimal CalibrationWeightMultiplier { get; set; } = 1.0m;

    [MaxLength(600)]
    public string FeedbackSummary { get; set; } = string.Empty;

    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
}
