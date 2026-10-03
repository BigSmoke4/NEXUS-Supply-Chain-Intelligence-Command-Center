using NEXUS.Modules.AssetManagement.Domain;

namespace NEXUS.Modules.AssetManagement.Application;

public sealed record AssetInspectionDto(
    Guid Id,
    string AssetCode,
    string Name,
    string AssetType,
    string Region,
    string Location,
    decimal Capacity,
    decimal CurrentLoad,
    decimal UtilizationPct,
    decimal UnitCostUsd,
    decimal ReliabilityPct,
    int LeadTimeDays,
    decimal RiskScore,
    string RiskLevel,
    decimal AvailabilityPct,
    decimal DailyRevenueExposureUsd,
    int DependentWarehousesCount,
    int DownstreamAssetsCount,
    string OperationalStatus,
    bool IsBottleneck,
    bool IsCriticalPathNode);

public interface IAssetManagementService
{
    Task<IReadOnlyList<AssetInspectionDto>> GetAssetsAsync(
        string? assetType = null,
        string? region = null,
        string? search = null,
        bool onlyHighRisk = false,
        bool onlyBottlenecks = false,
        int limit = 250,
        CancellationToken cancellationToken = default);

    Task<AssetInspectionDto?> GetAssetByIdOrCodeAsync(string idOrCode, CancellationToken cancellationToken = default);

    Task<AssetInspectionDto?> UpdateAssetCapacityAvailabilityAsync(
        string idOrCode,
        decimal availabilityPct,
        string actorName,
        CancellationToken cancellationToken = default);
}
