using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Execution.Domain;

public sealed class Execution : EntityBase
{
    [Required, MaxLength(64)]
    public string ExecutionCode { get; set; } = string.Empty;

    public Guid DecisionId { get; set; }

    [MaxLength(64)]
    public string DecisionCode { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string SelectedStrategyCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ActionSummary { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = "COMPLETED";

    public int ProgressPct { get; set; } = 100;

    [MaxLength(160)]
    public string ApprovedByUser { get; set; } = "Operations Manager";

    public DateTime InitiatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; } = DateTime.UtcNow;

    public string ReplayEventsJson { get; set; } = "[]";

    public List<ExecutionResult> Results { get; set; } = new();
}

public sealed class ExecutionResult : EntityBase
{
    public Guid ExecutionId { get; set; }

    [Required, MaxLength(64)]
    public string TargetAssetCode { get; set; } = string.Empty;

    [MaxLength(160)]
    public string TargetAssetName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string OperationPerformed { get; set; } = string.Empty;

    public decimal CapacityShiftedUnitsPerDay { get; set; }

    public decimal CostIncurredUsd { get; set; }

    public decimal PostExecutionUtilizationPct { get; set; }

    public decimal ServiceLevelAchievedPct { get; set; }

    public bool IsSuccessful { get; set; } = true;
}
