using NEXUS.Modules.SupplyChain.Domain;

namespace NEXUS.Modules.SupplyChain.Application;

public sealed record SupplyChainOverviewDto(
    int SuppliersCount,
    int FactoriesCount,
    int WarehousesCount,
    int DistributionCentersCount,
    int TransportationRoutesCount,
    int ProductsCount,
    int MarketsCount,
    int CustomersCount,
    decimal AverageSupplierReliabilityPct,
    decimal AverageFactoryUtilizationPct,
    decimal AverageWarehouseUtilizationPct,
    decimal AverageTransportUtilizationPct,
    IReadOnlyList<Supplier> TopSuppliers,
    IReadOnlyList<Factory> Factories,
    IReadOnlyList<Warehouse> TopWarehouses,
    IReadOnlyList<TransportationRoute> CriticalRoutes);

public interface ISupplyChainService
{
    Task<SupplyChainOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
