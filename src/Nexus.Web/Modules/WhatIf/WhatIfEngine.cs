using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.WhatIf;

public record WhatIfInput(Guid NodeId, double CapacityPercent, double AdditionalInventoryDays, double DemandMultiplierPercent);

public record WhatIfResult(
    decimal EstimatedRevenueAtRisk, int AffectedProductCount, double EstimatedStockoutDays,
    double EstimatedServiceLevelPercent, decimal EstimatedAdditionalCost);

/// <summary>
/// §57 "What-If Mode": lets a user drag capacity/inventory/demand sliders
/// and see an estimate update instantly. This is deliberately a fast,
/// simplified closed-form estimate - NOT a re-run of the day-by-day
/// SimulationEngine, which would be too slow for slider-drag responsiveness
/// at production data volumes. The trade-off is explicit: What-If gives an
/// instant approximation for exploration; running an actual Scenario through
/// ISimulationEngine remains the authoritative, deterministic calculation
/// (§10) used for the executive summary, mitigation comparison, and audit
/// trail. Never present a What-If number as if it were a real Scenario result.
/// </summary>
public interface IWhatIfEngine
{
    Task<WhatIfResult> CalculateAsync(Guid organizationId, WhatIfInput input, CancellationToken ct = default);
}

public class WhatIfEngine : IWhatIfEngine
{
    private readonly NexusDbContext _db;
    public WhatIfEngine(NexusDbContext db) => _db = db;

    public async Task<WhatIfResult> CalculateAsync(Guid organizationId, WhatIfInput input, CancellationToken ct = default)
    {
        var node = await _db.SupplyNodes.FirstOrDefaultAsync(n => n.Id == input.NodeId && n.OrganizationId == organizationId, ct);
        if (node is null) return new WhatIfResult(0, 0, 0, 100, 0);

        var edges = await _db.SupplyEdges.Where(e => e.OrganizationId == organizationId).ToListAsync(ct);
        var nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == organizationId).ToDictionaryAsync(n => n.Id, ct);
        var adjacency = edges.GroupBy(e => e.SourceNodeId).ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList());

        // BFS downstream, same traversal GraphService/SimulationEngine use,
        // reimplemented locally here to avoid a second DB round-trip through
        // the full GraphService abstraction on every slider tick.
        var affectedComponentIds = new HashSet<Guid>();
        var visited = new HashSet<Guid> { node.Id };
        var queue = new Queue<Guid>();
        queue.Enqueue(node.Id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;
            foreach (var next in neighbors)
            {
                if (!visited.Add(next)) continue;
                if (nodes.TryGetValue(next, out var n) && n.Type == NodeType.Component)
                    affectedComponentIds.Add(n.NodeRefId);
                queue.Enqueue(next);
            }
        }

        var boms = await _db.BillOfMaterials.Where(b => affectedComponentIds.Contains(b.ComponentId)).ToListAsync(ct);
        var affectedProductIds = boms.Select(b => b.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => affectedProductIds.Contains(p.Id)).ToListAsync(ct);

        var inventoryRecords = await _db.InventoryRecords
            .Where(i => affectedComponentIds.Contains(i.ComponentId) && i.OrganizationId == organizationId)
            .ToListAsync(ct);

        // Capacity reduction as a fraction (0 = no disruption, 1 = full shutdown).
        var reductionFraction = Math.Clamp(1 - input.CapacityPercent / 100.0, 0, 1);
        var demandFactor = input.DemandMultiplierPercent / 100.0;

        // Estimated days of coverage before stockout, given the additional
        // inventory the user is exploring and the capacity reduction's
        // effect on inbound replenishment - the same physics as §8, closed-
        // form instead of day-by-day.
        double estimatedStockoutDays = double.PositiveInfinity;
        foreach (var inv in inventoryRecords)
        {
            if (reductionFraction <= 0) continue; // no disruption, no depletion
            var effectiveInventory = inv.QuantityOnHand + (decimal)input.AdditionalInventoryDays * inv.DailyConsumption;
            var netDailyLoss = inv.DailyConsumption * (decimal)reductionFraction * (decimal)Math.Max(0.1, demandFactor);
            if (netDailyLoss <= 0) continue;
            var days = (double)(effectiveInventory / netDailyLoss);
            estimatedStockoutDays = Math.Min(estimatedStockoutDays, days);
        }
        if (double.IsPositiveInfinity(estimatedStockoutDays)) estimatedStockoutDays = 0;

        // Revenue exposure: affected products' daily revenue, scaled by
        // capacity reduction and demand factor, over a fixed 30-day
        // exploration horizon (What-If mode doesn't take a disruption
        // duration input - it's asking "what if this capacity level
        // persisted", not modeling a specific dated event).
        const int explorationHorizonDays = 30;
        var revenueAtRisk = products.Sum(p =>
            p.DailyDemandUnits * p.UnitRevenue * (decimal)reductionFraction * (decimal)demandFactor) * explorationHorizonDays;

        var serviceLevel = products.Count == 0 ? 100.0 : 100.0 * (1 - reductionFraction * 0.6);

        // Rough cost-to-mitigate estimate, consistent in magnitude with
        // MitigationEngine's air-freight strategy (~5% of exposure) - shown
        // so the slider view gives an at-a-glance mitigation cost trade-off.
        var estimatedCost = revenueAtRisk * 0.05m;

        return new WhatIfResult(
            Math.Round(revenueAtRisk, 0), products.Count, Math.Round(estimatedStockoutDays, 1),
            Math.Round(Math.Clamp(serviceLevel, 0, 100), 1), Math.Round(estimatedCost, 0));
    }
}
