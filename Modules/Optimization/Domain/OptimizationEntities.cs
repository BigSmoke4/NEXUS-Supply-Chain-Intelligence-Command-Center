using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Optimization.Domain;

public sealed class OptimizationRun : EntityBase
{
    public Guid ScenarioId { get; set; }

    public Guid? SimulationRunId { get; set; }

    [Required, MaxLength(64)]
    public string RunCode { get; set; } = string.Empty;

    [MaxLength(80)]
    public string SolverEngine { get; set; } = "Google OR-Tools GLOP / SCIP Multi-Objective Solver";

    [MaxLength(32)]
    public string SolverStatus { get; set; } = "OPTIMAL";

    public decimal ObjectiveValue { get; set; }

    public decimal OptimalCostUsd { get; set; }

    public decimal RevenueProtectedUsd { get; set; }

    public decimal ResultingServiceLevelPct { get; set; }

    public decimal RiskReductionPct { get; set; }

    public decimal ResultingResilienceScore { get; set; }

    public long SolveTimeMs { get; set; }

    public List<OptimizationConstraint> Constraints { get; set; } = new();

    public List<OptimizationResult> Results { get; set; } = new();
}

public sealed class OptimizationConstraint : EntityBase
{
    public Guid OptimizationRunId { get; set; }

    [Required, MaxLength(80)]
    public string ConstraintName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Category { get; set; } = "Capacity";

    public decimal LowerBound { get; set; }

    public decimal UpperBound { get; set; }

    public decimal EvaluatedActivity { get; set; }

    public decimal SlackValue { get; set; }

    public decimal ShadowPrice { get; set; }

    public bool IsBinding { get; set; }

    [MaxLength(32)]
    public string Unit { get; set; } = "units/day";
}

public sealed class OptimizationResult : EntityBase
{
    public Guid OptimizationRunId { get; set; }

    [Required, MaxLength(32)]
    public string StrategyCode { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string StrategyName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string CandidateActionType { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PrimaryAssetCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SecondaryAssetCode { get; set; } = string.Empty;

    public decimal ReallocationPct { get; set; }

    public decimal AllocatedUnitsPerDay { get; set; }

    public decimal TotalStrategyCostUsd { get; set; }

    public decimal AdditionalCostUsd { get; set; }

    public decimal RevenueProtectedUsd { get; set; }

    public decimal RiskReductionPct { get; set; }

    [MaxLength(32)]
    public string RiskLevel { get; set; } = "MEDIUM";

    public decimal ResultingServiceLevelPct { get; set; }

    public decimal ResilienceScore { get; set; }

    public bool IsParetoFrontierMember { get; set; } = true;

    public bool IsRecommended { get; set; }
}

public sealed class Mitigation : EntityBase
{
    [Required, MaxLength(64)]
    public string MitigationCode { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string InvestmentType { get; set; } = "Add Supplier";

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public decimal InvestmentCostUsd { get; set; }

    public decimal RiskReductionPct { get; set; }

    public decimal AnnualRevenueProtectedUsd { get; set; }

    public decimal ResiliencePointsGain { get; set; }

    public decimal RoiMultiple { get; set; }

    public bool IsOptimalSelection { get; set; }
}
