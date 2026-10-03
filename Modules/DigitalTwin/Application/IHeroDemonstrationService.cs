namespace NEXUS.Modules.DigitalTwin.Application;

public sealed record HeroDemoStepDto(
    int StepNumber,
    string Title,
    string ModuleName,
    string Status,
    string MetricSummary,
    string TargetUrl);

public sealed record HeroDemoExecutionReportDto(
    string SupplierCode,
    string SupplierName,
    decimal CapacityBeforePct,
    decimal CapacityAfterPct,
    int DurationDays,
    int PropagatedNodesCount,
    Guid ScenarioId,
    Guid SimulationRunId,
    decimal ProbabilityOfStockoutPct,
    decimal ProbabilityLossOver1MPct,
    decimal ProbabilitySlaBelow95Pct,
    Guid OptimizationRunId,
    Guid DecisionId,
    string DecisionCode,
    string RecommendedStrategy,
    string WhatExplanation,
    string WhyExplanation,
    string ExpectedResultSummary,
    Guid ExecutionId,
    decimal PredictedRevenueLossUsd,
    decimal ActualRevenueLossUsd,
    decimal PredictionErrorPct,
    decimal DecisionEffectivenessScore,
    decimal ResilienceScoreBefore,
    decimal ResilienceScoreAfter,
    IReadOnlyList<HeroDemoStepDto> Steps);

public interface IHeroDemonstrationService
{
    Task<HeroDemoExecutionReportDto> ExecuteFullHeroDemonstrationAsync(
        string actorName = "Operations Manager",
        CancellationToken cancellationToken = default);
}
