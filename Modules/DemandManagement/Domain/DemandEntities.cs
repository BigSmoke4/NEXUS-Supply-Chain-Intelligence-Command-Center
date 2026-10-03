using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.DemandManagement.Domain;

public sealed class DemandSnapshot : EntityBase
{
    public Guid MarketId { get; set; }

    [Required, MaxLength(64)]
    public string MarketCode { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    [Required, MaxLength(64)]
    public string ProductSku { get; set; } = string.Empty;

    public decimal ForecastedDemandUnits { get; set; }

    public decimal ActualOrderVolumeUnits { get; set; }

    public decimal FulfilledUnits { get; set; }

    public decimal BackorderUnits { get; set; }

    public decimal ServiceLevelPct { get; set; }

    public decimal RevenueUsd { get; set; }

    public DateTime SnapshotAtUtc { get; set; } = DateTime.UtcNow;
}
