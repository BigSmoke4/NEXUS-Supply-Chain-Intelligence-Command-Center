using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Inventory.Domain;

public sealed class InventorySnapshot : EntityBase
{
    public Guid WarehouseId { get; set; }

    [Required, MaxLength(64)]
    public string WarehouseCode { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    [Required, MaxLength(64)]
    public string ProductSku { get; set; } = string.Empty;

    public decimal OnHandUnits { get; set; }

    public decimal SafetyStockUnits { get; set; }

    public decimal ReorderPointUnits { get; set; }

    public decimal DailyConsumptionUnits { get; set; }

    public decimal CoverageDays { get; set; }

    public decimal HoldingCostPerDayUsd { get; set; }

    public DateTime SnapshotAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CapacitySnapshot : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string AssetCode { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string AssetType { get; set; } = string.Empty;

    public decimal NominalCapacity { get; set; }

    public decimal EffectiveCapacity { get; set; }

    public decimal CurrentLoad { get; set; }

    public decimal UtilizationPct { get; set; }

    public decimal SpareCapacityUnits { get; set; }

    public DateTime SnapshotAtUtc { get; set; } = DateTime.UtcNow;
}
