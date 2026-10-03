using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Modules.AssetManagement.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.AssetManagement.Infrastructure;

public sealed class AssetManagementService : IAssetManagementService
{
    private readonly NexusDbContext _db;
    private readonly INexusCacheService _cache;
    private readonly IAuditService _audit;

    public AssetManagementService(
        NexusDbContext db,
        INexusCacheService cache,
        IAuditService audit)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AssetInspectionDto>> GetAssetsAsync(
        string? assetType = null,
        string? region = null,
        string? search = null,
        bool onlyHighRisk = false,
        bool onlyBottlenecks = false,
        int limit = 250,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Assets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(assetType) && !string.Equals(assetType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.AssetType == assetType);
        }

        if (!string.IsNullOrWhiteSpace(region) && !string.Equals(region, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Region == region);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.AssetCode.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                x.Location.ToLower().Contains(term));
        }

        if (onlyHighRisk)
        {
            query = query.Where(x => x.RiskScore >= 60m || x.RiskLevel == "HIGH" || x.RiskLevel == "CRITICAL");
        }

        if (onlyBottlenecks)
        {
            query = query.Where(x => x.IsBottleneck || x.UtilizationPct >= 88m);
        }

        return await query
            .OrderByDescending(x => x.IsCriticalPathNode)
            .ThenByDescending(x => x.DailyRevenueExposureUsd)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(x => MapToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetInspectionDto?> GetAssetByIdOrCodeAsync(string idOrCode, CancellationToken cancellationToken = default)
    {
        Asset? asset = null;
        if (Guid.TryParse(idOrCode, out var guid))
        {
            asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == guid, cancellationToken);
        }

        asset ??= await _db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AssetCode == idOrCode.ToUpperInvariant(), cancellationToken);

        return asset is null ? null : MapToDto(asset);
    }

    public async Task<AssetInspectionDto?> UpdateAssetCapacityAvailabilityAsync(
        string idOrCode,
        decimal availabilityPct,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(availabilityPct, 5m, 150m);
        Asset? asset = null;
        if (Guid.TryParse(idOrCode, out var guid))
        {
            asset = await _db.Assets.FirstOrDefaultAsync(x => x.Id == guid, cancellationToken);
        }

        asset ??= await _db.Assets.FirstOrDefaultAsync(x => x.AssetCode == idOrCode.ToUpperInvariant(), cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var prevAvailability = asset.AvailabilityPct;
        asset.AvailabilityPct = clamped;
        asset.OperationalStatus = clamped < 75m ? "DEGRADED" : clamped < 95m ? "WARNING" : "ONLINE";
        if (clamped < 85m)
        {
            asset.RiskScore = Math.Min(99m, asset.RiskScore + (100m - clamped) * 0.45m);
            asset.RiskLevel = asset.RiskScore >= 70m ? "HIGH" : "MEDIUM";
            asset.IsBottleneck = true;
        }
        asset.UpdatedAtUtc = DateTime.UtcNow;
        asset.ConcurrencyStampVersion++;

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.SupplierCode == asset.AssetCode, cancellationToken);
        if (supplier is not null)
        {
            supplier.AvailabilityPct = clamped;
            supplier.CurrentOutputUnitsPerDay = Math.Round(supplier.CapacityUnitsPerDay * (clamped / 100m), 0);
            supplier.RiskScore = asset.RiskScore;
            supplier.RiskLevel = asset.RiskLevel;
        }

        var factory = await _db.Factories.FirstOrDefaultAsync(x => x.FactoryCode == asset.AssetCode, cancellationToken);
        if (factory is not null)
        {
            factory.AvailabilityPct = clamped;
            factory.RiskScore = asset.RiskScore;
            factory.RiskLevel = asset.RiskLevel;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            actorName,
            "OperationsManager",
            "AssetManagement",
            "ASSET_CAPACITY_AVAILABILITY_UPDATED",
            "Asset",
            asset.AssetCode,
            $"Updated {asset.AssetCode} ({asset.Name}) operational capacity availability from {prevAvailability:F1}% to {clamped:F1}%.");

        return MapToDto(asset);
    }

    private static AssetInspectionDto MapToDto(Asset x) => new(
        x.Id,
        x.AssetCode,
        x.Name,
        x.AssetType,
        x.Region,
        x.Location,
        x.Capacity,
        x.CurrentLoad,
        x.UtilizationPct,
        x.UnitCostUsd,
        x.ReliabilityPct,
        x.LeadTimeDays,
        x.RiskScore,
        x.RiskLevel,
        x.AvailabilityPct,
        x.DailyRevenueExposureUsd,
        x.DependentWarehousesCount,
        x.DownstreamAssetsCount,
        x.OperationalStatus,
        x.IsBottleneck,
        x.IsCriticalPathNode);
}
