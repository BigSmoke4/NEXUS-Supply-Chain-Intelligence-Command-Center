using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Modules.Simulation.Domain;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.Simulation.Infrastructure;

public sealed class SimulationService : ISimulationService
{
    private readonly NexusDbContext _db;
    private readonly IDependencyGraphService _graphService;
    private readonly INexusCacheService _cache;
    private readonly IAuditService _audit;

    public SimulationService(
        NexusDbContext db,
        IDependencyGraphService graphService,
        INexusCacheService cache,
        IAuditService audit)
    {
        _db = db;
        _graphService = graphService;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SimulationRun>> GetSimulationRunsAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        return await _db.SimulationRuns
            .AsNoTracking()
            .Include(x => x.Results)
            .OrderByDescending(x => x.ExecutedAtUtc)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<SimulationRun?> GetSimulationRunByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.SimulationRuns
            .AsNoTracking()
            .Include(x => x.Results.OrderBy(r => r.DayNumber).ThenBy(r => r.HopDepth))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SimulationRun> ExecuteSimulationAsync(
        RunSimulationCommand command,
        Action<int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        progressCallback?.Invoke(10, "Loading scenario and digital twin topology");

        var scenario = await _db.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.ScenarioId, cancellationToken)
            ?? await _db.Scenarios.AsNoTracking().FirstAsync(cancellationToken);

        int horizonDays = command.OverrideHorizonDays ?? scenario.DurationDays;
        horizonDays = Math.Clamp(horizonDays, 3, 90);
        int iterations = Math.Clamp(command.MonteCarloIterations, 100, 50_000);

        decimal capacityReductionPct = Math.Clamp(100m - scenario.SupplierCapacityMultiplierPct, 0m, 100m);

        progressCallback?.Invoke(25, "Running multi-hop dependency failure propagation");
        var impact = await _graphService.AnalyzeImpactAsync(
            scenario.TargetAssetCode,
            Math.Max(10m, capacityReductionPct),
            horizonDays,
            cancellationToken);

        progressCallback?.Invoke(50, $"Executing {iterations:N0} Monte Carlo stochastic futures");
        var mc = RunMonteCarloKernel(
            scenario.SupplierCapacityMultiplierPct,
            scenario.DemandDeltaPct,
            scenario.TransportCostDeltaPct,
            scenario.FactoryCapacityDeltaPct,
            horizonDays,
            baselineDailyDemandUnits: 14_500m,
            baselineInventoryUnits: 42_000m,
            unitRevenueUsd: 265m,
            iterations: iterations,
            randomSeed: command.RandomSeed);

        progressCallback?.Invoke(80, "Synthesizing day-by-day cascade trajectory");

        var simResults = new List<SimulationResult>();
        var topImpactedNodes = impact.PropagatedImpacts.Take(4).ToList();
        decimal runningInventory = 42_000m;
        int? firstStockoutDay = null;
        decimal cumulativeDetLoss = 0m;

        for (int day = 1; day <= horizonDays; day++)
        {
            decimal dailyShockRatio = (100m - scenario.SupplierCapacityMultiplierPct) / 100m;
            decimal dailyDemand = 14_500m * (1m + scenario.DemandDeltaPct / 100m);
            decimal dailySupply = 14_500m * (1m - dailyShockRatio * 0.68m) * (1m + scenario.FactoryCapacityDeltaPct / 100m);
            decimal netDrain = Math.Max(0m, dailyDemand - dailySupply);

            runningInventory = Math.Max(0m, runningInventory - netDrain);
            if (runningInventory <= 8_500m && firstStockoutDay is null)
            {
                firstStockoutDay = day;
            }

            decimal unfulfilled = runningInventory <= 8_500m ? netDrain * 0.72m : netDrain * 0.18m;
            decimal dailyLoss = Math.Round(unfulfilled * 265m, 0);
            cumulativeDetLoss += dailyLoss;
            decimal dailySla = Math.Clamp(Math.Round((1m - unfulfilled / Math.Max(1m, dailyDemand)) * 100m, 1), 65m, 99.8m);

            var stageAsset = topImpactedNodes.Count > 0
                ? topImpactedNodes[Math.Min(topImpactedNodes.Count - 1, (day - 1) * topImpactedNodes.Count / horizonDays)]
                : new PropagationNodeImpactDto(scenario.TargetAssetCode, scenario.TargetAssetName, "Supplier", 0, capacityReductionPct, scenario.SupplierCapacityMultiplierPct, dailyLoss, cumulativeDetLoss, "Direct capacity reduction");

            simResults.Add(new SimulationResult
            {
                OrganizationId = NexusSeedData.DefaultOrganizationId,
                DayNumber = day,
                AffectedAssetCode = stageAsset.AssetCode,
                AffectedAssetName = stageAsset.Name,
                AssetType = stageAsset.AssetType,
                HopDepth = stageAsset.HopDepth,
                EffectiveCapacityPct = stageAsset.ResultingEffectiveAvailabilityPct,
                InventoryRemainingUnits = Math.Round(runningInventory, 0),
                UnfulfilledDemandUnits = Math.Round(unfulfilled, 0),
                DailyRevenueLossUsd = dailyLoss,
                ServiceLevelPct = dailySla,
                CascadeDescription = day switch
                {
                    1 => $"{scenario.TargetAssetCode} capacity at {scenario.SupplierCapacityMultiplierPct:F0}%; upstream material buffer absorbing initial shock.",
                    <= 4 => $"Factory material receipts reduced; assembly utilization strained and WIP queues depleting.",
                    <= 8 => $"Regional warehouse inventory buffer breached; downstream fulfillment throttling active.",
                    _ => $"Customer order delays propagating across dependent markets; cumulative revenue loss ${cumulativeDetLoss / 1_000_000m:F2}M."
                }
            });
        }

        var propagationChain = topImpactedNodes.Select((node, idx) => new
        {
            Step = idx + 1,
            AssetCode = node.AssetCode,
            AssetName = node.Name,
            AssetType = node.AssetType,
            HopDepth = node.HopDepth,
            EffectiveAvailabilityPct = node.ResultingEffectiveAvailabilityPct,
            Impact = node.ImpactReason,
            LossUsd = node.CumulativeHorizonRevenueLossUsd
        }).ToList();

        var count = await _db.SimulationRuns.CountAsync(cancellationToken) + 1;
        var run = new SimulationRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            ScenarioId = scenario.Id,
            RunCode = $"SIM-{count:D3}-{DateTime.UtcNow:HHmmss}",
            ScenarioName = scenario.Name,
            TargetAssetCode = scenario.TargetAssetCode,
            HorizonDays = horizonDays,
            MonteCarloIterations = iterations,
            Status = "COMPLETED",
            DeterministicRevenueLossUsd = cumulativeDetLoss,
            DeterministicServiceLevelPct = simResults.Count > 0 ? Math.Round(simResults.Average(r => r.ServiceLevelPct), 1) : 90m,
            FirstStockoutDay = firstStockoutDay ?? horizonDays,
            RecoveryTimeDays = mc.ExpectedRecoveryDays,
            OperationalCostImpactUsd = mc.ExpectedOperationalCostUsd,
            MonteCarloMeanRevenueLossUsd = mc.MeanRevenueLossUsd,
            MonteCarloP50RevenueLossUsd = mc.P50RevenueLossUsd,
            MonteCarloP95RevenueLossUsd = mc.P95RevenueLossUsd,
            MonteCarloP99RevenueLossUsd = mc.P99RevenueLossUsd,
            ProbabilityOfStockoutPct = mc.ProbabilityOfStockoutPct,
            ProbabilityRevenueLossOver1MPct = mc.ProbabilityRevenueLossOver1MPct,
            ProbabilitySlaBelow95Pct = mc.ProbabilitySlaBelow95Pct,
            ExpectedServiceLevelPct = mc.ExpectedServiceLevelPct,
            ExpectedRecoveryDays = mc.ExpectedRecoveryDays,
            ExpectedOperationalCostUsd = mc.ExpectedOperationalCostUsd,
            HistogramBucketsJson = JsonSerializer.Serialize(mc.DistributionBuckets),
            PropagationChainJson = JsonSerializer.Serialize(propagationChain),
            ExecutedAtUtc = DateTime.UtcNow,
            Results = simResults
        };

        _db.SimulationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            command.InitiatedBy,
            "OperationsManager",
            "Simulation",
            "MONTE_CARLO_SIMULATION_COMPLETED",
            "SimulationRun",
            run.RunCode,
            $"Executed {iterations:N0}-future Monte Carlo & deterministic cascade simulation for '{scenario.Name}'. Mean Revenue Loss: ${mc.MeanRevenueLossUsd:N0}, Stockout Prob: {mc.ProbabilityOfStockoutPct:F1}%.");

        progressCallback?.Invoke(100, "Simulation complete");
        return run;
    }

    public MonteCarloSummaryDto RunMonteCarloKernel(
        decimal supplierCapacityMultiplierPct,
        decimal demandDeltaPct,
        decimal transportCostDeltaPct,
        decimal factoryCapacityDeltaPct,
        int nominalDurationDays,
        decimal baselineDailyDemandUnits,
        decimal baselineInventoryUnits,
        decimal unitRevenueUsd,
        int iterations = 10_000,
        int randomSeed = 20261003)
    {
        int n = Math.Clamp(iterations, 50, 50_000);
        var rng = new Random(randomSeed);

        var losses = new double[n];
        var slas = new double[n];
        var recoveryDaysArr = new double[n];
        var opCosts = new double[n];

        int stockoutCount = 0;
        int lossOver1MCount = 0;
        int slaBelow95Count = 0;

        double supCapRatio = Math.Clamp((double)supplierCapacityMultiplierPct / 100.0, 0.05, 1.5);
        double demandShift = 1.0 + ((double)demandDeltaPct / 100.0);
        double transportMult = 1.0 + ((double)transportCostDeltaPct / 100.0);
        double factoryMult = 1.0 + ((double)factoryCapacityDeltaPct / 100.0);
        double baseDemand = (double)baselineDailyDemandUnits;
        double baseInv = (double)baselineInventoryUnits;
        double unitRev = (double)unitRevenueUsd;

        for (int i = 0; i < n; i++)
        {
            // Box-Muller normal variates for uncertain variables (Section 14)
            double zDemand = NextGaussian(rng);
            double zReliability = NextGaussian(rng);
            double zLeadTime = NextGaussian(rng);
            double zTransportDelay = NextGaussian(rng);
            double zFactoryCap = NextGaussian(rng);
            double zDuration = NextGaussian(rng);

            double simDuration = Math.Max(2.0, nominalDurationDays * (1.0 + 0.14 * zDuration));
            double simDemandPerDay = Math.Max(100.0, baseDemand * demandShift * (1.0 + 0.09 * zDemand));
            double simSupplierReliability = Math.Clamp(0.91 + 0.05 * zReliability, 0.55, 0.999);
            double simFactoryEfficiency = Math.Clamp(factoryMult * (0.94 + 0.04 * zFactoryCap), 0.40, 1.25);
            double simLeadTimePenalty = Math.Max(0.0, 1.5 + 0.8 * zLeadTime + 0.9 * zTransportDelay);

            // Effective supply during disruption
            // Alternative multi-sourcing & safety buffer covers up to ~86% of baseline unless severe shock
            double effectiveSupplyRatio = Math.Clamp((supCapRatio * 0.52 + 0.48) * simSupplierReliability * simFactoryEfficiency, 0.25, 1.15);
            double dailySupply = baseDemand * effectiveSupplyRatio;
            double dailyDeficit = Math.Max(0.0, simDemandPerDay - dailySupply);

            double totalDeficitOverDuration = dailyDeficit * (simDuration + simLeadTimePenalty * 0.35);
            double usableBuffer = baseInv * 0.88;
            double unfulfilledUnits = Math.Max(0.0, totalDeficitOverDuration - usableBuffer);

            if (unfulfilledUnits > 0.0)
            {
                stockoutCount++;
            }

            double totalHorizonDemand = simDemandPerDay * simDuration;
            double achievedSla = Math.Clamp((1.0 - (unfulfilledUnits / Math.Max(1.0, totalHorizonDemand))) * 100.0, 50.0, 100.0);
            slas[i] = achievedSla;

            if (achievedSla < 95.0)
            {
                slaBelow95Count++;
            }

            // Revenue loss includes unfulfilled orders + expedite/spot procurement exposure
            double directRevenueLoss = unfulfilledUnits * unitRev;
            double disruptionExposureLoss = Math.Max(0.0, (totalDeficitOverDuration * 0.22) * (unitRev * 0.45));
            double totalRevLoss = directRevenueLoss + disruptionExposureLoss;
            losses[i] = totalRevLoss;

            if (totalRevLoss > 1_000_000.0 && unfulfilledUnits > baseDemand * 0.12)
            {
                lossOver1MCount++;
            }

            double recDays = Math.Max(1.5, (simDuration * 0.65) + simLeadTimePenalty + (unfulfilledUnits > 0 ? 3.2 : 0.0));
            recoveryDaysArr[i] = recDays;

            double opCost = (totalDeficitOverDuration * 42.0 * transportMult) + (simDuration * 38_000.0);
            opCosts[i] = opCost;
        }

        Array.Sort(losses);

        decimal meanLoss = Math.Round((decimal)losses.Average(), 0);
        decimal p50Loss = Math.Round((decimal)losses[(int)(n * 0.50)], 0);
        decimal p95Loss = Math.Round((decimal)losses[Math.Min(n - 1, (int)(n * 0.95))], 0);
        decimal p99Loss = Math.Round((decimal)losses[Math.Min(n - 1, (int)(n * 0.99))], 0);

        decimal probStockout = Math.Round((decimal)stockoutCount / n * 100m, 1);
        decimal probLoss1M = Math.Round((decimal)lossOver1MCount / n * 100m, 1);
        decimal probSla95 = Math.Round((decimal)slaBelow95Count / n * 100m, 1);
        decimal expSla = Math.Round((decimal)slas.Average(), 1);
        decimal expRec = Math.Round((decimal)recoveryDaysArr.Average(), 1);
        decimal expCost = Math.Round((decimal)opCosts.Average(), 0);

        // Build 7 histogram buckets
        double maxLoss = Math.Max(1_000_000.0, losses[Math.Min(n - 1, (int)(n * 0.98))]);
        double step = maxLoss / 7.0;
        var buckets = new List<HistogramBucketDto>(7);
        for (int b = 0; b < 7; b++)
        {
            double low = b * step;
            double high = b == 6 ? double.MaxValue : (b + 1) * step;
            int cnt = losses.Count(v => v >= low && v < high);
            string label = b == 6
                ? $">${low / 1_000_000.0:F1}M"
                : $"${low / 1_000_000.0:F1}M-${high / 1_000_000.0:F1}M";
            buckets.Add(new HistogramBucketDto(label, cnt, Math.Round((decimal)cnt / n * 100m, 1)));
        }

        return new MonteCarloSummaryDto(
            n,
            meanLoss,
            p50Loss,
            p95Loss,
            p99Loss,
            probStockout,
            probLoss1M,
            probSla95,
            expSla,
            expRec,
            expCost,
            buckets);
    }

    private static double NextGaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
