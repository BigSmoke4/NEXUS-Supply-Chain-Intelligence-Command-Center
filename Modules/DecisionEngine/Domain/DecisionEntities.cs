using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.DecisionEngine.Domain;

public static class CandidateActionTypes
{
    public const string SwitchSupplier = "Switch Supplier";
    public const string IncreaseProduction = "Increase Production";
    public const string MoveProduction = "Move Production";
    public const string IncreaseSafetyStock = "Increase Safety Stock";
    public const string ExpediteShipment = "Expedite Shipment";
    public const string ChangeTransportationRoute = "Change Transportation Route";
    public const string PrioritizeProduct = "Prioritize Product";
    public const string ReallocateInventory = "Reallocate Inventory";
    public const string ReduceLowPriorityDemand = "Reduce Low-Priority Demand";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SwitchSupplier,
        IncreaseProduction,
        MoveProduction,
        IncreaseSafetyStock,
        ExpediteShipment,
        ChangeTransportationRoute,
        PrioritizeProduct,
        ReallocateInventory,
        ReduceLowPriorityDemand
    };
}

public sealed class Decision : EntityBase
{
    [Required, MaxLength(64)]
    public string DecisionCode { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    public Guid? ScenarioId { get; set; }

    public Guid? SimulationRunId { get; set; }

    public Guid? OptimizationRunId { get; set; }

    [MaxLength(64)]
    public string RecommendedStrategyCode { get; set; } = "Strategy B";

    [MaxLength(600)]
    public string ProblemDetected { get; set; } = string.Empty;

    [MaxLength(600)]
    public string WhatRecommendation { get; set; } = string.Empty;

    [MaxLength(1500)]
    public string WhyExplanation { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string ExpectedResultSummary { get; set; } = string.Empty;

    public int CandidateStrategiesGenerated { get; set; } = 147;

    public int SimulationsCompleted { get; set; } = 10_000;

    public decimal RevenueProtectedUsd { get; set; }

    public decimal AdditionalCostUsd { get; set; }

    public decimal ProjectedServiceLevelPct { get; set; }

    public decimal RiskReductionPct { get; set; }

    public decimal ResilienceScoreBefore { get; set; }

    public decimal ResilienceScoreAfter { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "PENDING_APPROVAL";

    public bool RequiresHumanApproval { get; set; } = true;

    public string ReplayTimelineJson { get; set; } = "[]";

    public List<DecisionOption> Options { get; set; } = new();

    public List<DecisionApproval> Approvals { get; set; } = new();
}

public sealed class DecisionOption : EntityBase
{
    public Guid DecisionId { get; set; }

    [Required, MaxLength(64)]
    public string StrategyCode { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string StrategyName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ActionType { get; set; } = CandidateActionTypes.MoveProduction;

    [MaxLength(600)]
    public string Description { get; set; } = string.Empty;

    public decimal CostUsd { get; set; }

    [MaxLength(32)]
    public string RiskLabel { get; set; } = "MEDIUM";

    public decimal RiskScore { get; set; }

    public decimal ServiceLevelPct { get; set; }

    public decimal ResilienceScore { get; set; }

    public decimal RevenueProtectedUsd { get; set; }

    public decimal AdditionalCostUsd { get; set; }

    public decimal RiskReductionPct { get; set; }

    public bool IsRecommended { get; set; }
}

public sealed class DecisionApproval : EntityBase
{
    public Guid DecisionId { get; set; }

    public Guid? ReviewerId { get; set; }

    [Required, MaxLength(160)]
    public string ReviewerName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ReviewerRole { get; set; } = "OperationsManager";

    /// <summary>APPROVED or REJECTED</summary>
    [Required, MaxLength(32)]
    public string ApprovalStatus { get; set; } = "APPROVED";

    [MaxLength(600)]
    public string Comments { get; set; } = string.Empty;

    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;
}
