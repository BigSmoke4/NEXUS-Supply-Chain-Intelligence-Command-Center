using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.DigitalTwin.Domain;

public sealed class EnterpriseStateSnapshot : EntityBase
{
    public decimal DailyDemandUnits { get; set; }

    public decimal TotalInventoryUnits { get; set; }

    public decimal InventoryCoverageDays { get; set; }

    public decimal TotalCapacityUnits { get; set; }

    public decimal SupplierHealthPct { get; set; }

    public decimal FactoryUtilizationPct { get; set; }

    public decimal WarehouseUtilizationPct { get; set; }

    public decimal TransportCapacityPct { get; set; }

    public decimal DailyOrderVolumeUnits { get; set; }

    public decimal DailyRevenueUsd { get; set; }

    public decimal DailyCostUsd { get; set; }

    public decimal RevenueAtRiskUsd { get; set; }

    public decimal OverallRiskScore { get; set; }

    public decimal ServiceLevelPct { get; set; }

    public decimal ResilienceScore { get; set; }

    public int ActiveRisksCount { get; set; }

    public int ActiveScenariosCount { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Risk : EntityBase
{
    [Required, MaxLength(64)]
    public string RiskCode { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = "Supply Continuity";

    [MaxLength(32)]
    public string Severity { get; set; } = "HIGH";

    public decimal ProbabilityPct { get; set; }

    public decimal ImpactScore { get; set; }

    public decimal CompositeRiskScore { get; set; }

    public Guid? AffectedAssetId { get; set; }

    [MaxLength(64)]
    public string AffectedAssetCode { get; set; } = string.Empty;

    [MaxLength(160)]
    public string AffectedAssetName { get; set; } = string.Empty;

    public decimal RevenueExposureUsd { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "ACTIVE";

    [MaxLength(500)]
    public string RecommendedMitigation { get; set; } = string.Empty;
}
