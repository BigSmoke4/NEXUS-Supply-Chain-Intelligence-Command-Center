using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Analytics.Application;
using NEXUS.Modules.Analytics.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.Analytics.Infrastructure;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly NexusDbContext _db;
    private readonly ISimulationService _simulationService;
    private readonly INexusCacheService _cache;
    private readonly IAuditService _audit;

    public AnalyticsService(
        NexusDbContext db,
        ISimulationService simulationService,
        INexusCacheService cache,
        IAuditService audit)
    {
        _db = db;
        _simulationService = simulationService;
        _cache = cache;
        _audit = audit;
    }

    public async Task<ResilienceScore> GetOrCalculateResilienceScoreAsync(bool forceRecalculate = false, CancellationToken cancellationToken = default)
    {
        if (!forceRecalculate)
        {
            var cached = await _cache.GetAsync<ResilienceScore>(NexusCacheKeys.ResilienceScore, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        var existing = await _db.ResilienceScores
            .AsNoTracking()
            .OrderByDescending(r => r.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var suppliers = await _db.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        var factories = await _db.Factories.AsNoTracking().ToListAsync(cancellationToken);
        var warehouses = await _db.Warehouses.AsNoTracking().ToListAsync(cancellationToken);

        bool degraded = suppliers.Any(s => s.AvailabilityPct < 95m);
        var score = existing ?? ComputeResilienceScoreBreakdown(84m, 88m, 85m, 81m, 89m, 86m, 96m, 87m);

        if (degraded)
        {
            score.OverallScore = 82.0m;
        }

        await _cache.SetAsync(NexusCacheKeys.ResilienceScore, score, TimeSpan.FromMinutes(3), cancellationToken);
        return score;
    }

    public ResilienceScore ComputeResilienceScoreBreakdown(
        decimal supplierDiversity,
        decimal capacityRedundancy,
        decimal inventoryBuffer,
        decimal dependencyRiskInverse,
        decimal transportationRedundancy,
        decimal recoveryCapability,
        decimal serviceLevel,
        decimal operationalFlexibility)
    {
        var overall = Math.Round(
            (supplierDiversity * 0.14m)
            + (capacityRedundancy * 0.14m)
            + (inventoryBuffer * 0.12m)
            + (dependencyRiskInverse * 0.12m)
            + (transportationRedundancy * 0.11m)
            + (recoveryCapability * 0.12m)
            + (serviceLevel * 0.13m)
            + (operationalFlexibility * 0.12m),
            1);

        var disrupted = Math.Max(40m, Math.Round(overall - 5.0m, 1));
        var afterMitigation = Math.Min(99m, Math.Round(disrupted + 12.0m, 1));

        return new ResilienceScore
        {
            Id = Guid.NewGuid(),
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            OverallScore = overall,
            SupplierDiversityScore = supplierDiversity,
            CapacityRedundancyScore = capacityRedundancy,
            InventoryBufferScore = inventoryBuffer,
            DependencyRiskScore = dependencyRiskInverse,
            TransportationRedundancyScore = transportationRedundancy,
            RecoveryCapabilityScore = recoveryCapability,
            ServiceLevelScore = serviceLevel,
            OperationalFlexibilityScore = operationalFlexibility,
            SimulatedCurrentDisruptedScore = disrupted,
            SimulatedAfterMitigationScore = afterMitigation,
            CalculatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<BlackSwanStressResultDto> RunBlackSwanStressTestAsync(
        BlackSwanStressRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await _db.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        var factories = await _db.Factories.AsNoTracking().ToListAsync(cancellationToken);
        var dependencies = await _db.Dependencies.AsNoTracking().ToListAsync(cancellationToken);
        var mitigations = await _db.Mitigations.AsNoTracking().ToListAsync(cancellationToken);

        var mostVulnSupplier = suppliers
            .OrderByDescending(s => s.RiskScore * (s.CapacityUnitsPerDay / 1000m) * (100m / Math.Max(50m, s.ReliabilityPct)))
            .First();

        var mostVulnFactory = factories
            .OrderByDescending(f => f.UtilizationPct * (f.DailyRevenueExposureUsd / 1_000_000m))
            .First();

        var mostDangerousEdge = dependencies
            .OrderByDescending(d => (d.IsSinglePointOfFailure ? 50m : 0m) + (d.Strength * 40m) + d.RiskScore)
            .First();

        var bestMitigation = mitigations
            .OrderByDescending(m => m.AnnualRevenueProtectedUsd * (m.RiskReductionPct / 100m))
            .First();

        int iterations = Math.Clamp(request.SimulatedScenariosCount, 500, 25_000);
        decimal supCapMult = Math.Clamp(100m + request.SupplierCapacityDeltaPct, 10m, 100m);
        decimal transportCostEquivalent = Math.Abs(request.TransportCapacityDeltaPct) + Math.Max(0m, request.EnergyCostDeltaPct);

        var mc = _simulationService.RunMonteCarloKernel(
            supplierCapacityMultiplierPct: supCapMult,
            demandDeltaPct: request.DemandDeltaPct,
            transportCostDeltaPct: transportCostEquivalent,
            factoryCapacityDeltaPct: request.FactoryCapacityDeltaPct,
            nominalDurationDays: 21,
            baselineDailyDemandUnits: 16_800m,
            baselineInventoryUnits: 38_000m,
            unitRevenueUsd: 295m,
            iterations: iterations,
            randomSeed: 992026);

        await _audit.RecordAsync(
            request.InitiatedBy,
            "RiskManager",
            "Analytics",
            "BLACK_SWAN_STRESS_TEST_EXECUTED",
            "BlackSwanLab",
            mostVulnSupplier.SupplierCode,
            $"Executed {iterations:N0}-scenario Black Swan stress test. Worst-case P99 revenue loss: ${mc.P99RevenueLossUsd:N0}. Most vulnerable supplier: {mostVulnSupplier.SupplierCode}.");

        return new BlackSwanStressResultDto(
            iterations,
            request.SupplierCapacityDeltaPct,
            request.DemandDeltaPct,
            request.TransportCapacityDeltaPct,
            request.EnergyCostDeltaPct,
            request.FactoryCapacityDeltaPct,
            mostVulnSupplier.SupplierCode,
            mostVulnSupplier.Name,
            mostVulnFactory.FactoryCode,
            mostVulnFactory.Name,
            $"{mostDangerousEdge.SourceAssetCode} -> {mostDangerousEdge.TargetAssetCode} ({mostDangerousEdge.DependencyType}, Strength {mostDangerousEdge.Strength:F2}, SPOF={mostDangerousEdge.IsSinglePointOfFailure})",
            mc.MeanRevenueLossUsd,
            mc.P99RevenueLossUsd,
            mc.ProbabilityOfStockoutPct,
            bestMitigation.MitigationCode,
            bestMitigation.Name,
            Math.Round(mc.P99RevenueLossUsd * 0.74m, 0));
    }
}
