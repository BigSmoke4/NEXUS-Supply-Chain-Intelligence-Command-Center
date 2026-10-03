using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.SupplyChain.Domain;

public sealed class Supplier : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string SupplierCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string PrimaryMaterial { get; set; } = "Precision Semiconductor & Alloy Assemblies";

    public decimal CapacityUnitsPerDay { get; set; }

    public decimal CurrentOutputUnitsPerDay { get; set; }

    public decimal CostPerUnitUsd { get; set; }

    public decimal ReliabilityPct { get; set; }

    public int LeadTimeDays { get; set; }

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Region { get; set; } = "Europe";

    [MaxLength(64)]
    public string Country { get; set; } = "Germany";

    public decimal RiskScore { get; set; }

    [MaxLength(32)]
    public string RiskLevel { get; set; } = "MEDIUM";

    public decimal AvailabilityPct { get; set; } = 100m;

    public bool IsPrimaryEuropeanHeroSupplier { get; set; }
}

public sealed class Factory : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string FactoryCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal CapacityUnitsPerDay { get; set; }

    public decimal CurrentLoadUnitsPerDay { get; set; }

    public decimal UtilizationPct { get; set; }

    [MaxLength(300)]
    public string ProductsManufactured { get; set; } = "Industrial Robotics, Turbine Modules, Smart Grid Controllers";

    public decimal ProductionCostPerUnitUsd { get; set; }

    public int WorkforceCount { get; set; }

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Region { get; set; } = "Europe";

    public decimal AvailabilityPct { get; set; } = 100m;

    public decimal RiskScore { get; set; }

    [MaxLength(32)]
    public string RiskLevel { get; set; } = "MEDIUM";

    public decimal DailyRevenueExposureUsd { get; set; }

    public int DependentWarehousesCount { get; set; }
}

public sealed class Warehouse : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string WarehouseCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal StorageCapacityUnits { get; set; }

    public decimal CurrentInventoryUnits { get; set; }

    public decimal UtilizationPct { get; set; }

    public decimal ProcessingRateUnitsPerDay { get; set; }

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Region { get; set; } = "Europe";

    public decimal OperatingCostPerDayUsd { get; set; }

    public int CoverageDays { get; set; } = 19;

    public decimal AvailabilityPct { get; set; } = 100m;
}

public sealed class DistributionCenter : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string DcCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal ThroughputCapacityUnitsPerDay { get; set; }

    public decimal CurrentThroughputUnitsPerDay { get; set; }

    public decimal UtilizationPct { get; set; }

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Region { get; set; } = "Europe";

    public decimal OperatingCostPerDayUsd { get; set; }

    public decimal AvailabilityPct { get; set; } = 100m;
}

public sealed class TransportationRoute : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string RouteCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string OriginAssetCode { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string DestinationAssetCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string TransportMode { get; set; } = "Intermodal Freight / Sea-Air";

    public decimal CapacityUnitsPerDay { get; set; }

    public decimal CurrentLoadUnitsPerDay { get; set; }

    public decimal UtilizationPct { get; set; }

    public int TransitLeadTimeDays { get; set; }

    public decimal CostPerUnitUsd { get; set; }

    public decimal ReliabilityPct { get; set; }

    public decimal RiskScore { get; set; }

    public decimal AvailabilityPct { get; set; } = 100m;
}

public sealed class Product : EntityBase
{
    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = "Autonomous Control Systems";

    public decimal UnitPriceUsd { get; set; }

    public decimal UnitManufacturingCostUsd { get; set; }

    public decimal DailyDemandUnits { get; set; }

    public int TargetSafetyStockDays { get; set; } = 14;

    public decimal CriticalityScore { get; set; } = 85m;

    [MaxLength(64)]
    public string PrimaryFactoryCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SecondaryFactoryCode { get; set; } = string.Empty;
}

public sealed class Customer : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Segment { get; set; } = "Enterprise Tier-1 OEM";

    [MaxLength(64)]
    public string Region { get; set; } = "Europe";

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(64)]
    public string MarketCode { get; set; } = string.Empty;

    public decimal ContractSlaPct { get; set; } = 98.0m;

    public decimal DailyRevenueUsd { get; set; }

    public decimal SlaBreachPenaltyPerDayUsd { get; set; }
}

public sealed class Market : EntityBase
{
    public Guid AssetId { get; set; }

    [Required, MaxLength(64)]
    public string MarketCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Region { get; set; } = "Europe";

    [MaxLength(16)]
    public string Currency { get; set; } = "EUR";

    public decimal DailyDemandUnits { get; set; }

    public decimal DailyRevenuePotentialUsd { get; set; }

    public decimal GrowthRatePct { get; set; }

    public decimal VolatilityIndex { get; set; }
}
