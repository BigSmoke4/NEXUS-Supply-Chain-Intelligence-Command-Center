using NEXUS.Modules.DemandManagement.Domain;

namespace NEXUS.Modules.DemandManagement.Application;

public sealed record DemandImpactMetricDto(
    decimal BaselineDailyDemandUnits,
    decimal ShockedDailyDemandUnits,
    decimal FulfillableDailyUnits,
    decimal UnfulfilledDailyUnits,
    decimal ProjectedServiceLevelPct,
    decimal DailyRevenueLossUsd);

public interface IDemandService
{
    DemandImpactMetricDto CalculateDemandFulfillment(
        decimal baselineDailyDemandUnits,
        decimal demandShockDeltaPct,
        decimal availableDailySupplyUnits,
        decimal averageUnitRevenueUsd);

    Task<IReadOnlyList<DemandSnapshot>> GetLatestDemandSnapshotsAsync(CancellationToken cancellationToken = default);
}
