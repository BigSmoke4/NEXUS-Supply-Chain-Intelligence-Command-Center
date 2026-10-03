using NEXUS.Modules.DigitalTwin.Domain;

namespace NEXUS.Modules.DigitalTwin.Application;

public sealed record EnterpriseStateDto(
    decimal ResilienceScore,
    decimal RevenueAtRiskUsd,
    decimal SupplierHealthPct,
    decimal FactoryUtilizationPct,
    decimal WarehouseUtilizationPct,
    decimal TransportCapacityPct,
    decimal InventoryCoverageDays,
    decimal DailyDemandUnits,
    decimal TotalInventoryUnits,
    decimal TotalCapacityUnits,
    decimal DailyOrderVolumeUnits,
    decimal DailyRevenueUsd,
    decimal DailyCostUsd,
    decimal OverallRiskScore,
    decimal ServiceLevelPct,
    int ActiveRisksCount,
    int ActiveScenariosCount,
    int TotalSuppliers,
    int TotalFactories,
    int TotalWarehouses,
    int TotalRoutes,
    int TotalProducts,
    int TotalDependencies,
    DateTime CalculatedAtUtc);

public interface IDigitalTwinService
{
    Task<EnterpriseStateDto> GetCurrentEnterpriseStateAsync(bool forceRecalculate = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Risk>> GetActiveRisksAsync(CancellationToken cancellationToken = default);

    decimal CalculateCompositeRiskScore(
        decimal utilizationPct,
        decimal reliabilityPct,
        decimal availabilityPct,
        int leadTimeDays,
        bool isSinglePointOfFailure);
}
