using NEXUS.Modules.Analytics.Domain;

namespace NEXUS.Modules.Analytics.Application;

public sealed record BlackSwanStressRequestDto(
    decimal SupplierCapacityDeltaPct = -40m,
    decimal DemandDeltaPct = 35m,
    decimal TransportCapacityDeltaPct = -20m,
    decimal EnergyCostDeltaPct = 25m,
    decimal FactoryCapacityDeltaPct = -15m,
    int SimulatedScenariosCount = 2500,
    string InitiatedBy = "Risk Manager");

public sealed record BlackSwanStressResultDto(
    int ScenariosEvaluated,
    decimal SupplierCapacityDeltaPct,
    decimal DemandDeltaPct,
    decimal TransportCapacityDeltaPct,
    decimal EnergyCostDeltaPct,
    decimal FactoryCapacityDeltaPct,
    string MostVulnerableSupplierCode,
    string MostVulnerableSupplierName,
    string MostVulnerableFactoryCode,
    string MostVulnerableFactoryName,
    string MostDangerousDependencyDescription,
    decimal MeanRevenueLossUsd,
    decimal WorstCaseRevenueLossUsd,
    decimal ProbabilityOfSystemicStockoutPct,
    string MostEffectiveMitigationCode,
    string MostEffectiveMitigationName,
    decimal MitigationProtectedRevenueUsd);

public interface IAnalyticsService
{
    Task<ResilienceScore> GetOrCalculateResilienceScoreAsync(bool forceRecalculate = false, CancellationToken cancellationToken = default);

    ResilienceScore ComputeResilienceScoreBreakdown(
        decimal supplierDiversity,
        decimal capacityRedundancy,
        decimal inventoryBuffer,
        decimal dependencyRiskInverse,
        decimal transportationRedundancy,
        decimal recoveryCapability,
        decimal serviceLevel,
        decimal operationalFlexibility);

    Task<BlackSwanStressResultDto> RunBlackSwanStressTestAsync(
        BlackSwanStressRequestDto request,
        CancellationToken cancellationToken = default);
}
