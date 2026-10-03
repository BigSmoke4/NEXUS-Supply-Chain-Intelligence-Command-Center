using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.DependencyGraph.Domain;

public sealed class Dependency : EntityBase
{
    public Guid SourceAssetId { get; set; }

    [Required, MaxLength(64)]
    public string SourceAssetCode { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string SourceAssetName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string SourceAssetType { get; set; } = string.Empty;

    public Guid TargetAssetId { get; set; }

    [Required, MaxLength(64)]
    public string TargetAssetCode { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string TargetAssetName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string TargetAssetType { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string DependencyType { get; set; } = "MaterialFlow";

    /// <summary>Coupling strength in [0.0, 1.0].</summary>
    public decimal Strength { get; set; } = 0.85m;

    public decimal CapacityUnitsPerDay { get; set; }

    public decimal CurrentFlowUnitsPerDay { get; set; }

    public decimal RiskScore { get; set; }

    public decimal CostPerUnitUsd { get; set; }

    public int LeadTimeDays { get; set; }

    public bool IsSinglePointOfFailure { get; set; }

    public bool IsOnCriticalPath { get; set; }
}
