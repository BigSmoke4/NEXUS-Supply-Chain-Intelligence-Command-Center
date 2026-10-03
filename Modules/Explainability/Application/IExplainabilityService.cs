using NEXUS.Modules.Explainability.Domain;

namespace NEXUS.Modules.Explainability.Application;

public sealed record ExplainableRecommendationDto(
    Guid DecisionId,
    string DecisionCode,
    string WhatStatement,
    IReadOnlyList<string> WhyBullets,
    string ExpectedResultRevenueProtected,
    string ExpectedResultAdditionalCost,
    string ExpectedResultServiceLevel,
    string ExpectedResultRiskReduction,
    IReadOnlyList<string> BindingConstraints,
    IReadOnlyList<string> SensitivityInsights,
    decimal MathematicalConfidencePct);

public interface IExplainabilityService
{
    Task<ExplainableRecommendationDto?> GetExplanationForDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default);

    ExplainableRecommendationDto BuildStructuredExplanation(
        Guid decisionId,
        string decisionCode,
        decimal reallocationPct,
        decimal factoryAUtilizationPct,
        decimal factoryBUtilizationPct,
        decimal supplierCReliabilityPct,
        decimal revenueProtectedUsd,
        decimal additionalCostUsd,
        decimal serviceLevelPct,
        decimal riskReductionPct);
}
