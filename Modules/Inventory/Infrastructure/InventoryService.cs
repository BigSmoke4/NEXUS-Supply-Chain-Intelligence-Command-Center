using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Inventory.Application;
using NEXUS.Modules.Inventory.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.Inventory.Infrastructure;

public sealed class InventoryService : IInventoryService
{
    private readonly NexusDbContext _db;

    public InventoryService(NexusDbContext db)
    {
        _db = db;
    }

    public InventoryDepletionMetricDto CalculateDepletionProjection(
        string warehouseCode,
        decimal currentOnHandUnits,
        decimal safetyStockUnits,
        decimal dailyInboundUnits,
        decimal dailyDemandUnits,
        int horizonDays)
    {
        var netDrain = Math.Max(0m, dailyDemandUnits - dailyInboundUnits);
        var coverageDays = dailyDemandUnits <= 0m
            ? 999m
            : netDrain <= 0m
                ? Math.Round(currentOnHandUnits / dailyDemandUnits, 1)
                : Math.Round(currentOnHandUnits / netDrain, 1);

        int? stockoutDay = null;
        bool willBreachSafety = false;
        decimal remaining = currentOnHandUnits;

        for (int day = 1; day <= Math.Max(1, horizonDays); day++)
        {
            remaining = remaining + dailyInboundUnits - dailyDemandUnits;
            if (remaining < safetyStockUnits)
            {
                willBreachSafety = true;
            }

            if (remaining <= 0m && stockoutDay is null)
            {
                stockoutDay = day;
                remaining = 0m;
            }
        }

        return new InventoryDepletionMetricDto(
            warehouseCode,
            currentOnHandUnits,
            safetyStockUnits,
            netDrain,
            coverageDays,
            stockoutDay,
            willBreachSafety);
    }

    public async Task<IReadOnlyList<InventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.InventorySnapshots
            .AsNoTracking()
            .OrderBy(x => x.WarehouseCode)
            .ToListAsync(cancellationToken);
    }
}
