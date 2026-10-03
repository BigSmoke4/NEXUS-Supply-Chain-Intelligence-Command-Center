using NEXUS.Modules.Simulation.Domain;

namespace NEXUS.Modules.Simulation.Application;

public sealed record RunSimulationCommand(
    Guid ScenarioId,
    int MonteCarloIterations = 10_000,
    int? OverrideHorizonDays = null,
    int RandomSeed = 20261003,
    string InitiatedBy = "Operations Manager");

public sealed record HistogramBucketDto(
    string Bucket,
    int Count,
    decimal ProbabilityPct);

public sealed record MonteCarloSummaryDto(
    int Iterations,
    decimal MeanRevenueLossUsd,
    decimal P50RevenueLossUsd,
    decimal P95RevenueLossUsd,
    decimal P99RevenueLossUsd,
    decimal ProbabilityOfStockoutPct,
    decimal ProbabilityRevenueLossOver1MPct,
    decimal ProbabilitySlaBelow95Pct,
    decimal ExpectedServiceLevelPct,
    decimal ExpectedRecoveryDays,
    decimal ExpectedOperationalCostUsd,
    IReadOnlyList<HistogramBucketDto> DistributionBuckets);

public interface ISimulationService
{
    Task<IReadOnlyList<SimulationRun>> GetSimulationRunsAsync(int count = 25, CancellationToken cancellationToken = default);
    Task<SimulationRun?> GetSimulationRunByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimulationRun> ExecuteSimulationAsync(
        RunSimulationCommand command,
        Action<int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    MonteCarloSummaryDto RunMonteCarloKernel(
        decimal supplierCapacityMultiplierPct,
        decimal demandDeltaPct,
        decimal transportCostDeltaPct,
        decimal factoryCapacityDeltaPct,
        int nominalDurationDays,
        decimal baselineDailyDemandUnits,
        decimal baselineInventoryUnits,
        decimal unitRevenueUsd,
        int iterations = 10_000,
        int randomSeed = 20261003);
}
