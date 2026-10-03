using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Organization.Domain;

public sealed class EnterpriseOrganization : EntityBase
{
    [Required, MaxLength(64)]
    public string Code { get; set; } = "NEXUS-GLOBAL";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "NEXUS GLOBAL INDUSTRIES";

    [MaxLength(120)]
    public string Headquarters { get; set; } = "Zurich / Frankfurt / New York / Singapore";

    [MaxLength(16)]
    public string BaseCurrency { get; set; } = "USD";

    public decimal AnnualRevenueUsd { get; set; } = 18_500_000_000m;

    public decimal DailyOperatingBudgetUsd { get; set; } = 32_000_000m;

    public decimal TargetServiceLevelPct { get; set; } = 97.5m;

    public bool IsActive { get; set; } = true;
}
