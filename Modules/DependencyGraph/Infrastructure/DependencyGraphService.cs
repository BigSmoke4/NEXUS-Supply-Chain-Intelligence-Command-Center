using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.AssetManagement.Domain;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.DependencyGraph.Infrastructure;

public sealed class DependencyGraphService : IDependencyGraphService
{
    private readonly NexusDbContext _db;
    private readonly INexusCacheService _cache;

    public DependencyGraphService(NexusDbContext db, INexusCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<EnterpriseGraphDto> GetEnterpriseGraphAsync(
        int maxNodes = 160,
        string? assetTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var totalAssets = await _db.Assets.CountAsync(cancellationToken);
        var totalDeps = await _db.Dependencies.CountAsync(cancellationToken);

        var assetQuery = _db.Assets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(assetTypeFilter) && !string.Equals(assetTypeFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            assetQuery = assetQuery.Where(x => x.AssetType == assetTypeFilter);
        }

        var selectedAssets = await assetQuery
            .OrderByDescending(x => x.IsCriticalPathNode)
            .ThenByDescending(x => x.IsBottleneck)
            .ThenByDescending(x => x.DailyRevenueExposureUsd)
            .Take(Math.Clamp(maxNodes, 20, 600))
            .Select(x => new GraphNodeDto(
                x.Id,
                x.AssetCode,
                x.Name,
                x.AssetType,
                x.Region,
                x.Location,
                x.Capacity,
                x.CurrentLoad,
                x.UtilizationPct,
                x.RiskScore,
                x.RiskLevel,
                x.AvailabilityPct,
                x.DailyRevenueExposureUsd,
                x.DependentWarehousesCount,
                x.IsBottleneck,
                x.IsCriticalPathNode))
            .ToListAsync(cancellationToken);

        var codeSet = selectedAssets.Select(n => n.AssetCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allEdges = await _db.Dependencies
            .AsNoTracking()
            .Where(e => codeSet.Contains(e.SourceAssetCode) && codeSet.Contains(e.TargetAssetCode))
            .Take(800)
            .Select(e => new GraphEdgeDto(
                e.Id,
                e.SourceAssetCode,
                e.TargetAssetCode,
                e.DependencyType,
                e.Strength,
                e.CapacityUnitsPerDay,
                e.CurrentFlowUnitsPerDay,
                e.RiskScore,
                e.CostPerUnitUsd,
                e.LeadTimeDays,
                e.IsSinglePointOfFailure,
                e.IsOnCriticalPath))
            .ToListAsync(cancellationToken);

        var criticalPath = ComputeCriticalPath(selectedAssets, allEdges);
        var bottlenecks = DetectBottlenecks(selectedAssets, allEdges);

        return new EnterpriseGraphDto(
            selectedAssets,
            allEdges,
            criticalPath,
            bottlenecks,
            totalAssets,
            totalDeps);
    }

    public async Task<DependencyImpactAnalysisDto> AnalyzeImpactAsync(
        string assetIdOrCode,
        decimal capacityReductionPct = 40m,
        int durationDays = 14,
        CancellationToken cancellationToken = default)
    {
        var allAssets = await _db.Assets.AsNoTracking().ToListAsync(cancellationToken);
        var allEdges = await _db.Dependencies.AsNoTracking().ToListAsync(cancellationToken);

        Asset? root = null;
        if (Guid.TryParse(assetIdOrCode, out var guid))
        {
            root = allAssets.FirstOrDefault(a => a.Id == guid);
        }

        root ??= allAssets.FirstOrDefault(a => string.Equals(a.AssetCode, assetIdOrCode, StringComparison.OrdinalIgnoreCase))
                 ?? allAssets.FirstOrDefault(a => a.AssetCode == "SUP-001")
                 ?? allAssets.First();

        var assetByCode = allAssets.ToDictionary(a => a.AssetCode, StringComparer.OrdinalIgnoreCase);
        var adjacency = allEdges
            .GroupBy(e => e.SourceAssetCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var clampedReduction = Math.Clamp(capacityReductionPct, 5m, 100m);
        var clampedDuration = Math.Clamp(durationDays, 1, 90);

        // Multi-hop BFS failure propagation with edge-strength attenuation & inventory buffer damping
        var visitedDepth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [root.AssetCode] = 0
        };
        var propagatedShock = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [root.AssetCode] = clampedReduction
        };
        var parentOnCascade = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var queue = new Queue<string>();
        queue.Enqueue(root.AssetCode);

        while (queue.Count > 0)
        {
            var currentCode = queue.Dequeue();
            var currentDepth = visitedDepth[currentCode];
            if (currentDepth >= 5)
            {
                continue;
            }

            if (!adjacency.TryGetValue(currentCode, out var outgoing))
            {
                continue;
            }

            var upstreamShock = propagatedShock[currentCode];
            foreach (var edge in outgoing)
            {
                if (!assetByCode.TryGetValue(edge.TargetAssetCode, out var targetAsset))
                {
                    continue;
                }

                // Attenuation based on dependency strength and target spare capacity buffer
                var spareBufferFactor = targetAsset.UtilizationPct >= 88m ? 0.96m : 0.78m;
                var transmittedShock = Math.Round(upstreamShock * edge.Strength * spareBufferFactor, 2);
                if (transmittedShock < 3.0m)
                {
                    continue;
                }

                if (!propagatedShock.TryGetValue(targetAsset.AssetCode, out var existingShock) || transmittedShock > existingShock)
                {
                    propagatedShock[targetAsset.AssetCode] = transmittedShock;
                    visitedDepth[targetAsset.AssetCode] = currentDepth + 1;
                    parentOnCascade[targetAsset.AssetCode] = currentCode;
                    queue.Enqueue(targetAsset.AssetCode);
                }
            }
        }

        var impacts = new List<PropagationNodeImpactDto>();
        foreach (var kvp in propagatedShock.OrderBy(k => visitedDepth[k.Key]).ThenByDescending(k => k.Value))
        {
            var asset = assetByCode[kvp.Key];
            var depth = visitedDepth[kvp.Key];
            var reduction = kvp.Value;
            var effectiveAvail = Math.Max(0m, 100m - reduction);
            var dailyLoss = Math.Round(asset.DailyRevenueExposureUsd * (reduction / 100m) * (depth == 0 ? 0.35m : 0.22m), 0);
            var horizonLoss = Math.Round(dailyLoss * clampedDuration * 0.65m, 0);

            impacts.Add(new PropagationNodeImpactDto(
                asset.AssetCode,
                asset.Name,
                asset.AssetType,
                depth,
                reduction,
                effectiveAvail,
                dailyLoss,
                horizonLoss,
                depth == 0
                    ? $"Primary disruption source ({reduction:F1}% capacity loss for {clampedDuration} days)"
                    : $"Hop-{depth} dependency cascade from {parentOnCascade.GetValueOrDefault(asset.AssetCode, root.AssetCode)} ({reduction:F1}% effective throughput reduction)"));
        }

        int factoriesCount = impacts.Count(i => i.AssetType == nameof(EnterpriseAssetType.Factory));
        int warehousesCount = impacts.Count(i => i.AssetType == nameof(EnterpriseAssetType.Warehouse));
        int customersCount = impacts.Count(i => i.AssetType == nameof(EnterpriseAssetType.Customer));

        decimal totalDailyAtRisk = Math.Round(impacts.Take(12).Sum(i => i.DailyRevenueExposureUsd), 0);
        decimal totalHorizonAtRisk = Math.Round(totalDailyAtRisk * clampedDuration * 0.58m, 0);
        decimal projectedSla = Math.Clamp(Math.Round(97.5m - (clampedReduction * 0.22m), 1), 65.0m, 99.5m);

        var highestLeaf = impacts
            .Where(i => i.HopDepth > 0)
            .OrderByDescending(i => i.HopDepth)
            .ThenByDescending(i => i.CumulativeHorizonRevenueLossUsd)
            .FirstOrDefault()?.AssetCode ?? root.AssetCode;

        var cascadePath = new List<string>();
        var cursor = highestLeaf;
        while (!string.IsNullOrEmpty(cursor))
        {
            cascadePath.Add(cursor);
            if (!parentOnCascade.TryGetValue(cursor, out var p))
            {
                break;
            }
            cursor = p;
        }
        cascadePath.Reverse();

        return new DependencyImpactAnalysisDto(
            root.AssetCode,
            root.Name,
            root.AssetType,
            clampedReduction,
            clampedDuration,
            Math.Max(0, impacts.Count - 1),
            factoriesCount,
            warehousesCount,
            customersCount,
            totalDailyAtRisk,
            totalHorizonAtRisk,
            projectedSla,
            cascadePath,
            impacts.Take(35).ToList());
    }

    public IReadOnlyList<string> ComputeCriticalPath(
        IReadOnlyList<GraphNodeDto> nodes,
        IReadOnlyList<GraphEdgeDto> edges)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var nodeMap = nodes.ToDictionary(n => n.AssetCode, StringComparer.OrdinalIgnoreCase);
        var outgoing = edges
            .Where(e => nodeMap.ContainsKey(e.SourceCode) && nodeMap.ContainsKey(e.TargetCode))
            .GroupBy(e => e.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Dynamic programming over DAG stage hierarchy (Supplier -> Factory -> Warehouse -> DC -> Market -> Customer)
        var stageOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(EnterpriseAssetType.Supplier)] = 0,
            [nameof(EnterpriseAssetType.Factory)] = 1,
            [nameof(EnterpriseAssetType.TransportationRoute)] = 2,
            [nameof(EnterpriseAssetType.Warehouse)] = 3,
            [nameof(EnterpriseAssetType.DistributionCenter)] = 4,
            [nameof(EnterpriseAssetType.Market)] = 5,
            [nameof(EnterpriseAssetType.Customer)] = 6
        };

        var orderedNodes = nodes
            .OrderBy(n => stageOrder.GetValueOrDefault(n.AssetType, 7))
            .ThenByDescending(n => n.DailyRevenueExposureUsd)
            .ToList();

        var bestScore = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var predecessor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var n in orderedNodes)
        {
            bestScore[n.AssetCode] = (n.RiskScore * 10m) + (n.DailyRevenueExposureUsd / 10_000m);
        }

        foreach (var u in orderedNodes)
        {
            if (!outgoing.TryGetValue(u.AssetCode, out var adj))
            {
                continue;
            }

            int uStage = stageOrder.GetValueOrDefault(u.AssetType, 7);
            foreach (var edge in adj)
            {
                if (!nodeMap.TryGetValue(edge.TargetCode, out var v))
                {
                    continue;
                }

                int vStage = stageOrder.GetValueOrDefault(v.AssetType, 7);
                if (vStage <= uStage)
                {
                    continue;
                }

                decimal edgeWeight = (edge.Strength * 500m) + (edge.RiskScore * 8m) + (edge.LeadTimeDays * 25m)
                                     + (v.DailyRevenueExposureUsd / 10_000m);
                decimal candidate = bestScore[u.AssetCode] + edgeWeight;
                if (candidate > bestScore.GetValueOrDefault(v.AssetCode, 0m))
                {
                    bestScore[v.AssetCode] = candidate;
                    predecessor[v.AssetCode] = u.AssetCode;
                }
            }
        }

        var endNode = bestScore.OrderByDescending(k => k.Value).First().Key;
        var path = new List<string>();
        var curr = endNode;
        var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrEmpty(curr) && guard.Add(curr))
        {
            path.Add(curr);
            if (!predecessor.TryGetValue(curr, out var prev))
            {
                break;
            }
            curr = prev;
        }

        path.Reverse();
        return path;
    }

    public IReadOnlyList<string> DetectBottlenecks(
        IReadOnlyList<GraphNodeDto> nodes,
        IReadOnlyList<GraphEdgeDto> edges)
    {
        var outDegree = edges
            .GroupBy(e => e.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var spofSources = edges
            .Where(e => e.IsSinglePointOfFailure)
            .Select(e => e.SourceCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return nodes
            .Select(n =>
            {
                int deg = outDegree.GetValueOrDefault(n.AssetCode, 0);
                bool isSpof = spofSources.Contains(n.AssetCode);
                decimal saturationScore = (n.UtilizationPct * 0.55m) + (n.RiskScore * 0.30m) + (deg * 3.5m) + (isSpof ? 25m : 0m);
                return (n.AssetCode, n.IsBottleneck, n.UtilizationPct, isSpof, Score: saturationScore);
            })
            .Where(x => x.IsBottleneck || x.UtilizationPct >= 88m || x.isSpof || x.Score >= 85m)
            .OrderByDescending(x => x.Score)
            .Take(15)
            .Select(x => x.AssetCode)
            .ToList();
    }
}
