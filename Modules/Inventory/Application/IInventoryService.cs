using NEXUS.Modules.Inventory.Domain;

namespace NEXUS.Modules.Inventory.Application;

public sealed record InventoryDepletionMetricDto(
    string WarehouseCode,
    decimal CurrentOnHandUnits,
    decimal SafetyStockUnits,
    decimal DailyNetDrainUnits,
    decimal CoverageDays,
    int? StockoutDay,
    bool WillBreachSafetyStock);

public interface IInventoryService
{
    InventoryDepletionMetricDto CalculateDepletionProjection(
        string warehouseCode,
        decimal currentOnHandUnits,
        decimal safetyStockUnits,
        decimal dailyInboundUnits,
        decimal dailyDemandUnits,
        int horizonDays);

    Task<IReadOnlyList<InventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default);
}
