using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.DemandManagement.Application;
using NEXUS.Modules.DemandManagement.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.DemandManagement.Infrastructure;

public sealed class DemandService : IDemandService
{
    private readonly NexusDbContext _db;

    public DemandService(NexusDbContext db)
    {
        _db = db;
    }

    public DemandImpactMetricDto CalculateDemandFulfillment(
        decimal baselineDailyDemandUnits,
        decimal demandShockDeltaPct,
        decimal availableDailySupplyUnits,
        decimal averageUnitRevenueUsd)
    {
        var shockedDemand = Math.Max(1m, Math.Round(baselineDailyDemandUnits * (1m + demandShockDeltaPct / 100m), 2));
        var fulfillable = Math.Clamp(availableDailySupplyUnits, 0m, shockedDemand);
        var unfulfilled = Math.Max(0m, shockedDemand - fulfillable);
        var slaPct = Math.Round((fulfillable / shockedDemand) * 100m, 2);
        var dailyLoss = Math.Round(unfulfilled * averageUnitRevenueUsd, 2);

        return new DemandImpactMetricDto(
            baselineDailyDemandUnits,
            shockedDemand,
            fulfillable,
            unfulfilled,
            slaPct,
            dailyLoss);
    }

    public async Task<IReadOnlyList<DemandSnapshot>> GetLatestDemandSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.DemandSnapshots
            .AsNoTracking()
            .OrderBy(x => x.MarketCode)
            .ToListAsync(cancellationToken);
    }
}
