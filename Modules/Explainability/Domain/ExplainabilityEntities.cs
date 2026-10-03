using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Explainability.Domain;

public sealed class DecisionExplanation : EntityBase
{
    public Guid DecisionId { get; set; }

    [Required, MaxLength(500)]
    public string WhatStatement { get; set; } = string.Empty;

    [Required, MaxLength(1500)]
    public string WhyStatement { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string ExpectedResultStatement { get; set; } = string.Empty;

    public string BindingConstraintsJson { get; set; } = "[]";

    public string SensitivityAnalysisJson { get; set; } = "[]";

    public decimal MathematicalConfidencePct { get; set; } = 96.4m;
}
