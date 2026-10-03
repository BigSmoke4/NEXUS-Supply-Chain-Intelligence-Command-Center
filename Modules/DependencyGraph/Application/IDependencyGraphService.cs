using NEXUS.Modules.AssetManagement.Application;

namespace NEXUS.Modules.DependencyGraph.Application;

public sealed record GraphNodeDto(
    Guid Id,
    string AssetCode,
    string Name,
    string AssetType,
    string Region,
    string Location,
    decimal Capacity,
    decimal CurrentLoad,
    decimal UtilizationPct,
    decimal RiskScore,
    string RiskLevel,
    decimal AvailabilityPct,
    decimal DailyRevenueExposureUsd,
    int DependentWarehousesCount,
    bool IsBottleneck,
    bool IsCriticalPathNode);

public sealed record GraphEdgeDto(
    Guid Id,
    string SourceCode,
    string TargetCode,
    string DependencyType,
    decimal Strength,
    decimal Capacity,
    decimal CurrentFlow,
    decimal RiskScore,
    decimal CostPerUnitUsd,
    int LeadTimeDays,
    bool IsSinglePointOfFailure,
    bool IsOnCriticalPath);

public sealed record EnterpriseGraphDto(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges,
    IReadOnlyList<string> CriticalPathAssetCodes,
    IReadOnlyList<string> BottleneckAssetCodes,
    int TotalAssetsCount,
    int TotalDependenciesCount);

public sealed record PropagationNodeImpactDto(
    string AssetCode,
    string Name,
    string AssetType,
    int HopDepth,
    decimal PropagatedCapacityReductionPct,
    decimal ResultingEffectiveAvailabilityPct,
    decimal DailyRevenueExposureUsd,
    decimal CumulativeHorizonRevenueLossUsd,
    string ImpactReason);

public sealed record DependencyImpactAnalysisDto(
    string RootAssetCode,
    string RootAssetName,
    string RootAssetType,
    decimal AppliedCapacityReductionPct,
    int DurationDays,
    int TotalAffectedDownstreamNodes,
    int AffectedFactoriesCount,
    int AffectedWarehousesCount,
    int AffectedCustomersCount,
    decimal DailyRevenueAtRiskUsd,
    decimal TotalHorizonRevenueAtRiskUsd,
    decimal ProjectedEnterpriseSlaPct,
    IReadOnlyList<string> CriticalCascadePath,
    IReadOnlyList<PropagationNodeImpactDto> PropagatedImpacts);

public interface IDependencyGraphService
{
    Task<EnterpriseGraphDto> GetEnterpriseGraphAsync(
        int maxNodes = 160,
        string? assetTypeFilter = null,
        CancellationToken cancellationToken = default);

    Task<DependencyImpactAnalysisDto> AnalyzeImpactAsync(
        string assetIdOrCode,
        decimal capacityReductionPct = 40m,
        int durationDays = 14,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> ComputeCriticalPath(
        IReadOnlyList<GraphNodeDto> nodes,
        IReadOnlyList<GraphEdgeDto> edges);

    IReadOnlyList<string> DetectBottlenecks(
        IReadOnlyList<GraphNodeDto> nodes,
        IReadOnlyList<GraphEdgeDto> edges);
}
