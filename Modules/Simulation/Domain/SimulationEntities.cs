using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Simulation.Domain;

public sealed class SimulationRun : EntityBase
{
    public Guid ScenarioId { get; set; }

    [Required, MaxLength(64)]
    public string RunCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ScenarioName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string TargetAssetCode { get; set; } = string.Empty;

    public int HorizonDays { get; set; } = 14;

    public int MonteCarloIterations { get; set; } = 10_000;

    [MaxLength(32)]
    public string Status { get; set; } = "COMPLETED";

    public decimal DeterministicRevenueLossUsd { get; set; }

    public decimal DeterministicServiceLevelPct { get; set; }

    public int FirstStockoutDay { get; set; }

    public decimal RecoveryTimeDays { get; set; }

    public decimal OperationalCostImpactUsd { get; set; }

    public decimal MonteCarloMeanRevenueLossUsd { get; set; }

    public decimal MonteCarloP50RevenueLossUsd { get; set; }

    public decimal MonteCarloP95RevenueLossUsd { get; set; }

    public decimal MonteCarloP99RevenueLossUsd { get; set; }

    public decimal ProbabilityOfStockoutPct { get; set; }

    public decimal ProbabilityRevenueLossOver1MPct { get; set; }

    public decimal ProbabilitySlaBelow95Pct { get; set; }

    public decimal ExpectedServiceLevelPct { get; set; }

    public decimal ExpectedRecoveryDays { get; set; }

    public decimal ExpectedOperationalCostUsd { get; set; }

    /// <summary>JSON array of histogram buckets for Monte Carlo distribution rendering.</summary>
    public string HistogramBucketsJson { get; set; } = "[]";

    /// <summary>JSON array of propagation steps for cascade visualization.</summary>
    public string PropagationChainJson { get; set; } = "[]";

    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

    public List<SimulationResult> Results { get; set; } = new();
}

public sealed class SimulationResult : EntityBase
{
    public Guid SimulationRunId { get; set; }

    public int DayNumber { get; set; }

    [Required, MaxLength(64)]
    public string AffectedAssetCode { get; set; } = string.Empty;

    [MaxLength(160)]
    public string AffectedAssetName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AssetType { get; set; } = string.Empty;

    public int HopDepth { get; set; }

    public decimal EffectiveCapacityPct { get; set; }

    public decimal InventoryRemainingUnits { get; set; }

    public decimal UnfulfilledDemandUnits { get; set; }

    public decimal DailyRevenueLossUsd { get; set; }

    public decimal ServiceLevelPct { get; set; }

    [MaxLength(300)]
    public string CascadeDescription { get; set; } = string.Empty;
}
