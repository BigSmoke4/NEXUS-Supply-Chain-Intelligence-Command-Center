using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.ScenarioManagement.Domain;

public static class ScenarioTypes
{
    public const string SupplierFailure = "Supplier Failure";
    public const string FactoryShutdown = "Factory Shutdown";
    public const string WarehouseClosure = "Warehouse Closure";
    public const string TransportationDisruption = "Transportation Disruption";
    public const string DemandSpike = "Demand Spike";
    public const string DemandCollapse = "Demand Collapse";
    public const string RawMaterialShortage = "Raw Material Shortage";
    public const string EnergyPriceIncrease = "Energy Price Increase";
    public const string LaborShortage = "Labor Shortage";
    public const string CurrencyShock = "Currency Shock";
    public const string CapacityReduction = "Capacity Reduction";
    public const string GeopoliticalDisruption = "Geopolitical Disruption";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SupplierFailure,
        FactoryShutdown,
        WarehouseClosure,
        TransportationDisruption,
        DemandSpike,
        DemandCollapse,
        RawMaterialShortage,
        EnergyPriceIncrease,
        LaborShortage,
        CurrencyShock,
        CapacityReduction,
        GeopoliticalDisruption
    };
}

public sealed class Scenario : EntityBase
{
    [Required, MaxLength(64)]
    public string ScenarioCode { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string ScenarioType { get; set; } = ScenarioTypes.CapacityReduction;

    [MaxLength(800)]
    public string Description { get; set; } = string.Empty;

    public Guid? TargetAssetId { get; set; }

    [MaxLength(64)]
    public string TargetAssetCode { get; set; } = string.Empty;

    [MaxLength(160)]
    public string TargetAssetName { get; set; } = string.Empty;

    public int DurationDays { get; set; } = 14;

    public decimal SupplierCapacityMultiplierPct { get; set; } = 60m;

    public decimal DemandDeltaPct { get; set; } = 0m;

    public decimal TransportCostDeltaPct { get; set; } = 0m;

    public decimal FactoryCapacityDeltaPct { get; set; } = 0m;

    public decimal EnergyCostDeltaPct { get; set; } = 0m;

    public bool IsHeroScenario { get; set; }

    public bool IsBlackSwanScenario { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "ACTIVE";

    [MaxLength(120)]
    public string CreatedByUser { get; set; } = "System Architect";

    public List<ScenarioParameter> Parameters { get; set; } = new();
}

public sealed class ScenarioParameter : EntityBase
{
    public Guid ScenarioId { get; set; }

    [Required, MaxLength(80)]
    public string ParameterName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string TargetAssetCode { get; set; } = string.Empty;

    public decimal BaselineValue { get; set; }

    public decimal ScenarioValue { get; set; }

    public decimal DeltaPercent { get; set; }

    [MaxLength(32)]
    public string Unit { get; set; } = "%";
}
