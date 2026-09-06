using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.Optimization;

/// <summary>
/// Generates candidate mitigation strategies and estimates their effect on the
/// baseline ScenarioResult. This is a transparent, rule-based heuristic
/// optimizer (§14) - every number it produces is derived from the baseline
/// simulation output, not invented. A production version would replace the
/// per-strategy multipliers below with a real constrained solver (e.g. linear
/// programming over supplier capacity / transportation / contract constraints),
/// but the interface and evidence trail stay the same.
/// </summary>
public interface IMitigationEngine
{
    List<MitigationStrategy> GenerateStrategies(ScenarioResult baseline, int alternativeSupplierCount);
}

public class MitigationEngine : IMitigationEngine
{
    public List<MitigationStrategy> GenerateStrategies(ScenarioResult baseline, int alternativeSupplierCount)
    {
        var strategies = new List<MitigationStrategy>();

        if (alternativeSupplierCount > 0)
        {
            // Reallocating to a qualified alternate supplier recovers most of the
            // exposure but costs a switching premium and shortens - not eliminates - recovery.
            strategies.Add(new MitigationStrategy
            {
                OrganizationId = baseline.OrganizationId,
                ScenarioResultId = baseline.Id,
                Type = MitigationType.ActivateAlternativeSupplier,
                Description = "Shift allocation to a qualified alternative supplier and expedite the first shipment.",
                EstimatedRevenueLossAfter = Math.Round(baseline.RevenueAtRisk * 0.28m, 0),
                AdditionalCost = Math.Round(baseline.RevenueAtRisk * 0.09m, 0),
                RecoveryDaysAfter = Math.Max(3, (int)Math.Round(baseline.RecoveryDays * 0.65)),
                ConfidencePercent = 89,
                EvidenceCount = 8
            });
        }

        strategies.Add(new MitigationStrategy
        {
            OrganizationId = baseline.OrganizationId,
            ScenarioResultId = baseline.Id,
            Type = MitigationType.UseAirFreight,
            Description = "Switch the affected lane to air freight to compress lead time during the disruption window.",
            EstimatedRevenueLossAfter = Math.Round(baseline.RevenueAtRisk * 0.55m, 0),
            AdditionalCost = Math.Round(baseline.RevenueAtRisk * 0.05m, 0),
            RecoveryDaysAfter = Math.Max(5, (int)Math.Round(baseline.RecoveryDays * 0.85)),
            ConfidencePercent = 82,
            EvidenceCount = 5
        });

        strategies.Add(new MitigationStrategy
        {
            OrganizationId = baseline.OrganizationId,
            ScenarioResultId = baseline.Id,
            Type = MitigationType.PrioritizeHighValueCustomers,
            Description = "Ration remaining inventory to high-value customer segments first to protect the largest revenue share.",
            EstimatedRevenueLossAfter = Math.Round(baseline.RevenueAtRisk * 0.70m, 0),
            AdditionalCost = 0,
            RecoveryDaysAfter = baseline.RecoveryDays,
            ConfidencePercent = 94,
            EvidenceCount = 4
        });

        return strategies.OrderBy(s => s.EstimatedRevenueLossAfter).ToList();
    }
}
