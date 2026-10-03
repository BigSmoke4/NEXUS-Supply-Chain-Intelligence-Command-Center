using NEXUS.Modules.Forecasting.Domain;

namespace NEXUS.Modules.Forecasting.Application;

public sealed record ModelEvaluationMetricDto(
    string TargetDomain,
    string Algorithm,
    int TrainingSamples,
    int ValidationSamples,
    decimal R2Score,
    decimal Mae,
    decimal Rmse,
    decimal RocAuc,
    decimal F1Score,
    IReadOnlyDictionary<string, double> FeatureImportances);

public sealed record RunPredictionRequestDto(
    string TargetDomain,
    string TargetAssetCode,
    int HorizonDays = 14,
    decimal UtilizationPct = 87m,
    decimal SupplierReliabilityPct = 91m,
    decimal InventoryCoverageDays = 19m,
    decimal DemandGrowthPct = 8m,
    string InitiatedBy = "Analyst");

public interface IForecastingService
{
    Task<IReadOnlyList<Prediction>> GetLatestPredictionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forecast>> GetDemandForecastHorizonAsync(int days = 14, CancellationToken cancellationToken = default);
    Task<Prediction> RunPredictionPipelineAsync(RunPredictionRequestDto request, CancellationToken cancellationToken = default);
    ModelEvaluationMetricDto TrainAndEvaluateRegressionAndClassification(string targetDomain, int sampleCount = 1200, int seed = 20261003);
}
