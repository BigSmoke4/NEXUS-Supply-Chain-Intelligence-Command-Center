using NEXUS.Modules.Optimization.Domain;

namespace NEXUS.Modules.Optimization.Application;

public sealed record RunOptimizationCommand(
    Guid ScenarioId,
    Guid? SimulationRunId = null,
    decimal BudgetCeilingUsd = 2_200_000m,
    decimal MinimumSlaTargetPct = 95.0m,
    decimal CostWeight = 0.35m,
    decimal ResilienceWeight = 0.65m,
    string InitiatedBy = "Operations Manager");

public sealed record MitigationPortfolioResultDto(
    decimal BudgetLimitUsd,
    decimal TotalInvestmentCostUsd,
    decimal CombinedRiskReductionPct,
    decimal TotalRevenueProtectedUsd,
    decimal BaselineResilienceScore,
    decimal ProjectedResilienceScore,
    decimal PortfolioRoiMultiple,
    string BestSingleInvestmentCode,
    string BestSingleInvestmentName,
    IReadOnlyList<Mitigation> EvaluatedMitigations);

public interface IOptimizationService
{
    Task<IReadOnlyList<OptimizationRun>> GetOptimizationRunsAsync(int count = 20, CancellationToken cancellationToken = default);
    Task<OptimizationRun?> GetOptimizationRunByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OptimizationRun> ExecuteMultiObjectiveOptimizationAsync(
        RunOptimizationCommand command,
        Action<int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    Task<MitigationPortfolioResultDto> OptimizeMitigationPortfolioAsync(
        decimal budgetLimitUsd = 2_800_000m,
        CancellationToken cancellationToken = default);
}
