using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.SupplyChain.Application;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.SupplyChain.Infrastructure;

public sealed class SupplyChainService : ISupplyChainService
{
    private readonly NexusDbContext _db;

    public SupplyChainService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<SupplyChainOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await _db.Suppliers.AsNoTracking().OrderBy(x => x.SupplierCode).ToListAsync(cancellationToken);
        var factories = await _db.Factories.AsNoTracking().OrderBy(x => x.FactoryCode).ToListAsync(cancellationToken);
        var warehouses = await _db.Warehouses.AsNoTracking().OrderBy(x => x.WarehouseCode).ToListAsync(cancellationToken);
        var dcsCount = await _db.DistributionCenters.CountAsync(cancellationToken);
        var routes = await _db.TransportationRoutes.AsNoTracking().OrderByDescending(x => x.RiskScore).Take(20).ToListAsync(cancellationToken);
        var routesCount = await _db.TransportationRoutes.CountAsync(cancellationToken);
        var productsCount = await _db.Products.CountAsync(cancellationToken);
        var marketsCount = await _db.Markets.CountAsync(cancellationToken);
        var customersCount = await _db.Customers.CountAsync(cancellationToken);

        return new SupplyChainOverviewDto(
            suppliers.Count,
            factories.Count,
            warehouses.Count,
            dcsCount,
            routesCount,
            productsCount,
            marketsCount,
            customersCount,
            suppliers.Count == 0 ? 91m : Math.Round(suppliers.Average(s => s.ReliabilityPct * (s.AvailabilityPct / 100m)), 1),
            factories.Count == 0 ? 87m : Math.Round(factories.Average(f => f.UtilizationPct), 1),
            warehouses.Count == 0 ? 72m : Math.Round(warehouses.Average(w => w.UtilizationPct), 1),
            routes.Count == 0 ? 81m : Math.Round(routes.Average(r => r.UtilizationPct), 1),
            suppliers.Take(15).ToList(),
            factories,
            warehouses.Take(15).ToList(),
            routes);
    }
}
