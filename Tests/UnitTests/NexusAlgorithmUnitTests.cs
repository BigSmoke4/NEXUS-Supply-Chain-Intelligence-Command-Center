using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Analytics.Infrastructure;
using NEXUS.Modules.Audit.Infrastructure;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.DecisionEngine.Infrastructure;
using NEXUS.Modules.DemandManagement.Infrastructure;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.DependencyGraph.Domain;
using NEXUS.Modules.DependencyGraph.Infrastructure;
using NEXUS.Modules.DigitalTwin.Infrastructure;
using NEXUS.Modules.Explainability.Infrastructure;
using NEXUS.Modules.Feedback.Infrastructure;
using NEXUS.Modules.Forecasting.Domain;
using NEXUS.Modules.Forecasting.Infrastructure;
using NEXUS.Modules.Inventory.Infrastructure;
using NEXUS.Modules.Optimization.Application;
using NEXUS.Modules.Optimization.Domain;
using NEXUS.Modules.Optimization.Infrastructure;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Modules.ScenarioManagement.Infrastructure;
using NEXUS.Modules.Simulation.Infrastructure;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;
using Xunit;

namespace NEXUS.UnitTests;

public sealed class NexusAlgorithmUnitTests
{
    private static NexusDbContext CreateInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase($"NexusUnitTestDb_{Guid.NewGuid()}")
            .Options;
        return new NexusDbContext(opts);
    }

    private static RedisNexusCacheService CreateCache()
    {
        var mem = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new RedisNexusCacheService(mem, NullLogger<RedisNexusCacheService>.Instance);
    }

    [Fact]
    public void GraphTraversal_And_CriticalPath_And_BottleneckDetection_IdentifyCorrectTopology()
    {
        using var db = CreateInMemoryDb();
        var service = new DependencyGraphService(db, CreateCache());

        var nodes = new List<GraphNodeDto>
        {
            new(Guid.NewGuid(), "SUP-001", "Supplier A", "Supplier", "Europe", "Frankfurt", 15000, 13800, 92m, 78m, "HIGH", 100m, 2400000m, 8, true, true),
            new(Guid.NewGuid(), "FAC-001", "Factory A", "Factory", "Europe", "Stuttgart", 14000, 12880, 92m, 68m, "HIGH", 100m, 2200000m, 8, true, true),
            new(Guid.NewGuid(), "WH-001", "Warehouse 1", "Warehouse", "Europe", "Frankfurt", 30000, 21600, 72m, 35m, "MEDIUM", 100m, 950000m, 0, false, true),
            new(Guid.NewGuid(), "CUST-001", "Customer 1", "Customer", "Europe", "Munich", 5000, 4100, 82m, 20m, "LOW", 100m, 850000m, 0, false, true)
        };

        var edges = new List<GraphEdgeDto>
        {
            new(Guid.NewGuid(), "SUP-001", "FAC-001", "MaterialFlow", 0.95m, 8500m, 7800m, 78m, 44m, 4, true, true),
            new(Guid.NewGuid(), "FAC-001", "WH-001", "FinishedGoods", 0.88m, 6000m, 5100m, 60m, 18m, 3, true, true),
            new(Guid.NewGuid(), "WH-001", "CUST-001", "Delivery", 0.90m, 4000m, 3500m, 25m, 8m, 1, false, true)
        };

        var criticalPath = service.ComputeCriticalPath(nodes, edges);
        var bottlenecks = service.DetectBottlenecks(nodes, edges);

        Assert.Equal(4, criticalPath.Count);
        Assert.Equal("SUP-001", criticalPath[0]);
        Assert.Equal("CUST-001", criticalPath[^1]);
        Assert.Contains("SUP-001", bottlenecks);
        Assert.Contains("FAC-001", bottlenecks);
    }

    [Fact]
    public async Task FailurePropagation_And_DependencyAnalysis_PropagatesMultiHopDegradation()
    {
        using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var service = new DependencyGraphService(db, cache);

        var sup = new NEXUS.Modules.AssetManagement.Domain.Asset
        {
            AssetCode = "SUP-001",
            Name = "RheinMetall Supplier A",
            AssetType = "Supplier",
            UtilizationPct = 92m,
            DailyRevenueExposureUsd = 2_450_000m
        };
        var fac = new NEXUS.Modules.AssetManagement.Domain.Asset
        {
            AssetCode = "FAC-001",
            Name = "Stuttgart Factory A",
            AssetType = "Factory",
            UtilizationPct = 92m,
            DailyRevenueExposureUsd = 2_200_000m
        };
        var wh = new NEXUS.Modules.AssetManagement.Domain.Asset
        {
            AssetCode = "WH-001",
            Name = "Frankfurt Hub WH-001",
            AssetType = "Warehouse",
            UtilizationPct = 78m,
            DailyRevenueExposureUsd = 1_100_000m
        };

        db.Assets.AddRange(sup, fac, wh);
        db.Dependencies.AddRange(
            new Dependency
            {
                SourceAssetId = sup.Id,
                SourceAssetCode = "SUP-001",
                SourceAssetName = sup.Name,
                SourceAssetType = "Supplier",
                TargetAssetId = fac.Id,
                TargetAssetCode = "FAC-001",
                TargetAssetName = fac.Name,
                TargetAssetType = "Factory",
                Strength = 0.95m,
                IsSinglePointOfFailure = true
            },
            new Dependency
            {
                SourceAssetId = fac.Id,
                SourceAssetCode = "FAC-001",
                SourceAssetName = fac.Name,
                SourceAssetType = "Factory",
                TargetAssetId = wh.Id,
                TargetAssetCode = "WH-001",
                TargetAssetName = wh.Name,
                TargetAssetType = "Warehouse",
                Strength = 0.85m
            });
        await db.SaveChangesAsync();

        var impact = await service.AnalyzeImpactAsync("SUP-001", 40m, 14);

        Assert.Equal("SUP-001", impact.RootAssetCode);
        Assert.Equal(2, impact.TotalAffectedDownstreamNodes);
        Assert.Equal(1, impact.AffectedFactoriesCount);
        Assert.Equal(1, impact.AffectedWarehousesCount);
        Assert.True(impact.TotalHorizonRevenueAtRiskUsd > 0m);
        Assert.Equal(new[] { "SUP-001", "FAC-001", "WH-001" }, impact.CriticalCascadePath);
    }

    [Fact]
    public void DemandCalculation_ComputesShockedDemandFulfillmentAndSla()
    {
        using var db = CreateInMemoryDb();
        var demandService = new DemandService(db);

        var res = demandService.CalculateDemandFulfillment(
            baselineDailyDemandUnits: 10_000m,
            demandShockDeltaPct: 30m,
            availableDailySupplyUnits: 11_700m,
            averageUnitRevenueUsd: 250m);

        Assert.Equal(13_000m, res.ShockedDailyDemandUnits);
        Assert.Equal(11_700m, res.FulfillableDailyUnits);
        Assert.Equal(1_300m, res.UnfulfilledDailyUnits);
        Assert.Equal(90.0m, res.ProjectedServiceLevelPct);
        Assert.Equal(325_000m, res.DailyRevenueLossUsd);
    }

    [Fact]
    public void InventoryCalculation_DetectsSafetyStockBreachAndExactStockoutDay()
    {
        using var db = CreateInMemoryDb();
        var inventoryService = new InventoryService(db);

        var res = inventoryService.CalculateDepletionProjection(
            warehouseCode: "WH-001",
            currentOnHandUnits: 12_000m,
            safetyStockUnits: 4_000m,
            dailyInboundUnits: 3_000m,
            dailyDemandUnits: 5_000m,
            horizonDays: 14);

        Assert.Equal(2_000m, res.DailyNetDrainUnits);
        Assert.Equal(6.0m, res.CoverageDays);
        Assert.Equal(6, res.StockoutDay);
        Assert.True(res.WillBreachSafetyStock);
    }

    [Fact]
    public void RiskScoring_IncreasesMonotonicallyWithUtilizationAndSpof()
    {
        using var db = CreateInMemoryDb();
        var twinService = new DigitalTwinService(db, CreateCache());

        var lowRisk = twinService.CalculateCompositeRiskScore(60m, 98m, 100m, 3, isSinglePointOfFailure: false);
        var highRisk = twinService.CalculateCompositeRiskScore(92m, 85m, 60m, 10, isSinglePointOfFailure: true);

        Assert.True(highRisk > lowRisk);
        Assert.InRange(lowRisk, 0m, 40m);
        Assert.InRange(highRisk, 65m, 100m);
    }

    [Fact]
    public void MonteCarloSimulation_GeneratesStochasticDistributionAndProbabilities()
    {
        using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var audit = new AuditService(db);
        var graph = new DependencyGraphService(db, cache);
        var simService = new SimulationService(db, graph, cache, audit);

        var mcMild = simService.RunMonteCarloKernel(
            supplierCapacityMultiplierPct: 90m,
            demandDeltaPct: 0m,
            transportCostDeltaPct: 0m,
            factoryCapacityDeltaPct: 0m,
            nominalDurationDays: 7,
            baselineDailyDemandUnits: 14_500m,
            baselineInventoryUnits: 45_000m,
            unitRevenueUsd: 265m,
            iterations: 2000,
            randomSeed: 42);

        var mcSevere = simService.RunMonteCarloKernel(
            supplierCapacityMultiplierPct: 40m,
            demandDeltaPct: 35m,
            transportCostDeltaPct: 25m,
            factoryCapacityDeltaPct: -15m,
            nominalDurationDays: 21,
            baselineDailyDemandUnits: 14_500m,
            baselineInventoryUnits: 25_000m,
            unitRevenueUsd: 265m,
            iterations: 2000,
            randomSeed: 42);

        Assert.Equal(2000, mcMild.Iterations);
        Assert.Equal(7, mcSevere.DistributionBuckets.Count);
        Assert.True(mcSevere.MeanRevenueLossUsd > mcMild.MeanRevenueLossUsd);
        Assert.True(mcSevere.P95RevenueLossUsd >= mcSevere.P50RevenueLossUsd);
        Assert.True(mcSevere.ProbabilityOfStockoutPct > mcMild.ProbabilityOfStockoutPct);
    }

    [Fact]
    public async Task OptimizationConstraints_GoogleOrToolsSolvesParetoStrategiesAndMitigationPortfolio()
    {
        using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var audit = new AuditService(db);
        var optService = new OptimizationService(db, cache, audit);

        var scenario = new Scenario
        {
            Id = Guid.NewGuid(),
            ScenarioCode = "SCN-TEST",
            Name = "Hero Test Scenario",
            TargetAssetCode = "SUP-001",
            DurationDays = 14,
            SupplierCapacityMultiplierPct = 60m,
            IsHeroScenario = true
        };
        db.Scenarios.Add(scenario);
        db.Mitigations.AddRange(
            new Mitigation { MitigationCode = "MIT-01", Name = "Add Supplier C", InvestmentType = "Add Supplier", InvestmentCostUsd = 680_000m, RiskReductionPct = 48m, AnnualRevenueProtectedUsd = 6_400_000m, ResiliencePointsGain = 6.2m, RoiMultiple = 9.4m },
            new Mitigation { MitigationCode = "MIT-02", Name = "Factory B Flex Line", InvestmentType = "Add Factory Capacity", InvestmentCostUsd = 850_000m, RiskReductionPct = 44m, AnnualRevenueProtectedUsd = 7_900_000m, ResiliencePointsGain = 5.8m, RoiMultiple = 9.3m }
        );
        await db.SaveChangesAsync();

        var run = await optService.ExecuteMultiObjectiveOptimizationAsync(
            new RunOptimizationCommand(scenario.Id, BudgetCeilingUsd: 2_200_000m, MinimumSlaTargetPct: 95.0m));

        Assert.Equal("OPTIMAL", run.SolverStatus);
        Assert.Equal(3, run.Results.Count);
        Assert.True(run.Constraints.Count >= 5);
        Assert.Contains(run.Results, r => r.StrategyCode == "Strategy B" && r.IsRecommended);

        var portfolio = await optService.OptimizeMitigationPortfolioAsync(2_000_000m);
        Assert.True(portfolio.TotalInvestmentCostUsd <= 2_000_000m);
        Assert.True(portfolio.TotalRevenueProtectedUsd > 0m);
        Assert.True(portfolio.ProjectedResilienceScore > portfolio.BaselineResilienceScore);
    }

    [Fact]
    public void DecisionRanking_OrdersStrategiesByMultiAttributeUtility()
    {
        using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var audit = new AuditService(db);
        var graph = new DependencyGraphService(db, cache);
        var scn = new ScenarioService(db, cache, audit);
        var sim = new SimulationService(db, graph, cache, audit);
        var opt = new OptimizationService(db, cache, audit);
        var exp = new ExplainabilityService(db);
        var decisionService = new DecisionEngineService(db, scn, graph, sim, opt, exp, cache, audit);

        var options = new List<DecisionOption>
        {
            new() { StrategyCode = "Strategy A", CostUsd = 1_200_000m, RiskScore = 68m, ServiceLevelPct = 88m, ResilienceScore = 84m, RevenueProtectedUsd = 5_400_000m },
            new() { StrategyCode = "Strategy B", CostUsd = 1_500_000m, RiskScore = 29m, ServiceLevelPct = 96m, ResilienceScore = 94m, RevenueProtectedUsd = 8_700_000m },
            new() { StrategyCode = "Strategy C", CostUsd = 1_900_000m, RiskScore = 14m, ServiceLevelPct = 99m, ResilienceScore = 96.5m, RevenueProtectedUsd = 8_980_000m }
        };

        var ranked = decisionService.RankDecisionOptions(options);

        Assert.Equal(3, ranked.Count);
        Assert.NotEqual("Strategy A", ranked[0].StrategyCode);
    }

    [Fact]
    public void ResilienceScoring_And_DecisionQualityFeedback_ComputeAccurateScores()
    {
        using var db = CreateInMemoryDb();
        var cache = CreateCache();
        var audit = new AuditService(db);
        var graph = new DependencyGraphService(db, cache);
        var sim = new SimulationService(db, graph, cache, audit);
        var analytics = new AnalyticsService(db, sim, cache, audit);
        var feedback = new FeedbackService(db, audit);

        var resScore = analytics.ComputeResilienceScoreBreakdown(84m, 88m, 85m, 81m, 89m, 86m, 96m, 87m);
        Assert.InRange(resScore.OverallScore, 85m, 90m);
        Assert.True(resScore.SimulatedAfterMitigationScore > resScore.SimulatedCurrentDisruptedScore);

        var quality = feedback.CalculateDecisionQualityMetrics(
            Guid.NewGuid(),
            "DEC-HERO-001",
            Guid.NewGuid(),
            predictedRevenueLossUsd: 420_000m,
            actualRevenueLossUsd: 390_000m,
            predictedCostUsd: 420_000m,
            actualCostUsd: 408_500m,
            predictedSlaPct: 98.2m,
            actualSlaPct: 98.5m);

        Assert.Equal(7.14m, quality.PredictionErrorPct);
        Assert.True(quality.DecisionEffectivenessScore > 90m);
        Assert.Equal(-11_500m, quality.CostVarianceUsd);
    }

    [Fact]
    public void PredictionEvaluation_CrossValidatesRealR2MaeRmseRocAucAndF1()
    {
        using var db = CreateInMemoryDb();
        var audit = new AuditService(db);
        var config = new ConfigurationBuilder().Build();
        var forecasting = new ForecastingService(db, new DummyHttpClientFactory(), config, audit, NullLogger<ForecastingService>.Instance);

        foreach (var domain in PredictionTargets.All)
        {
            var metrics = forecasting.TrainAndEvaluateRegressionAndClassification(domain, sampleCount: 800, seed: 2026);
            Assert.Equal(640, metrics.TrainingSamples);
            Assert.Equal(160, metrics.ValidationSamples);
            Assert.InRange(metrics.R2Score, 0.70m, 0.999m);
            Assert.True(metrics.Mae > 0m);
            Assert.True(metrics.Rmse >= metrics.Mae);
            Assert.InRange(metrics.RocAuc, 0.75m, 0.999m);
            Assert.InRange(metrics.F1Score, 0.70m, 0.999m);
            Assert.Equal(5, metrics.FeatureImportances.Count);
        }
    }

    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
