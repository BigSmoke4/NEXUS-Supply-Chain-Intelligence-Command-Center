using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Forecasting.Domain;

public static class PredictionTargets
{
    public const string Demand = "Demand";
    public const string SupplierFailure = "Supplier Failure";
    public const string InventoryShortage = "Inventory Shortage";
    public const string TransportationDelay = "Transportation Delay";
    public const string RecoveryTime = "Recovery Time";
    public const string SlaBreach = "SLA Breach";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Demand,
        SupplierFailure,
        InventoryShortage,
        TransportationDelay,
        RecoveryTime,
        SlaBreach
    };
}

public sealed class Prediction : EntityBase
{
    [Required, MaxLength(64)]
    public string PredictionCode { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string TargetDomain { get; set; } = PredictionTargets.Demand;

    [MaxLength(64)]
    public string TargetAssetCode { get; set; } = string.Empty;

    [MaxLength(160)]
    public string TargetAssetName { get; set; } = string.Empty;

    public int HorizonDays { get; set; } = 14;

    public decimal PredictedValue { get; set; }

    [MaxLength(32)]
    public string Unit { get; set; } = "units/day";

    public decimal ProbabilityPct { get; set; }

    public decimal ConfidenceIntervalLow { get; set; }

    public decimal ConfidenceIntervalHigh { get; set; }

    [MaxLength(80)]
    public string ModelAlgorithm { get; set; } = "XGBoost / GradientBoostingRegressor";

    public decimal ValidationR2 { get; set; }

    public decimal ValidationMae { get; set; }

    public decimal ValidationRmse { get; set; }

    public decimal ValidationRocAuc { get; set; }

    public decimal ValidationF1 { get; set; }

    public int TrainingSamplesCount { get; set; } = 5000;

    public string FeatureImportanceJson { get; set; } = "{}";

    public DateTime PredictedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Forecast : EntityBase
{
    [Required, MaxLength(64)]
    public string ForecastCode { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string MetricName { get; set; } = PredictionTargets.Demand;

    [MaxLength(64)]
    public string TargetCode { get; set; } = string.Empty;

    public int DayOffset { get; set; }

    public DateTime ForecastDateUtc { get; set; }

    public decimal BaselineValue { get; set; }

    public decimal ForecastedValue { get; set; }

    public decimal LowerBound { get; set; }

    public decimal UpperBound { get; set; }

    [MaxLength(64)]
    public string ModelSource { get; set; } = "NEXUS-ML-XGBoost-v2.1";
}
