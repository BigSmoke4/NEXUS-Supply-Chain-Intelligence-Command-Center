using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Risk;

namespace Nexus.Web.Controllers;

public record HeatmapCell(string EntityName, string EntityType, double Probability, double ImpactScore, decimal RevenueExposureEstimate);

[Authorize(Policy = NexusPermissions.SupplyNetworkRead)]
public class RiskController : Controller
{
    private readonly NexusDbContext _db;
    private readonly IRiskScoringService _riskScoring;

    public RiskController(NexusDbContext db, IRiskScoringService riskScoring)
    {
        _db = db;
        _riskScoring = riskScoring;
    }

    /// <summary>
    /// Builds a real Probability x Impact grid (§35) from data already in
    /// the domain model - not invented placement. Probability comes from
    /// the supplier's composite risk score (Modules/Risk), normalized to
    /// 0-1. Impact comes from revenue exposure: for each supplier, the sum
    /// of DailyDemandUnits * UnitRevenue across every product whose BOM
    /// depends (directly or transitively, via the graph) on a component
    /// that supplier provides - i.e. "how much revenue would be exposed if
    /// this supplier failed", the same quantity the simulation engine
    /// computes for an actual disruption, just precomputed for every
    /// supplier at once instead of one at a time.
    /// </summary>
    public async Task<IActionResult> Heatmap(CancellationToken ct)
    {
        var org = await _db.Organizations.FirstAsync(ct);

        var suppliers = await _db.Suppliers.Where(s => s.OrganizationId == org.Id).ToListAsync(ct);
        var supplierNodes = await _db.SupplyNodes
            .Where(n => n.OrganizationId == org.Id && n.Type == NodeType.Supplier)
            .ToListAsync(ct);
        var edges = await _db.SupplyEdges.Where(e => e.OrganizationId == org.Id).ToListAsync(ct);
        var nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == org.Id).ToDictionaryAsync(n => n.Id, ct);
        var boms = await _db.BillOfMaterials.ToListAsync(ct);
        var products = await _db.Products.Where(p => p.OrganizationId == org.Id).ToDictionaryAsync(p => p.Id, ct);

        var adjacency = edges.GroupBy(e => e.SourceNodeId).ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList());

        var cells = new List<HeatmapCell>();
        foreach (var supplier in suppliers)
        {
            var supplierNode = supplierNodes.FirstOrDefault(n => n.NodeRefId == supplier.Id);
            if (supplierNode is null) continue;

            // BFS downstream from this supplier to find every Component node
            // it reaches, exactly as SimulationEngine does for a real
            // disruption - reused here so the heatmap's numbers are
            // consistent with what a scenario against this supplier would show.
            var affectedComponentIds = new HashSet<Guid>();
            var visited = new HashSet<Guid> { supplierNode.Id };
            var queue = new Queue<Guid>();
            queue.Enqueue(supplierNode.Id);
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

            var affectedProductIds = boms.Where(b => affectedComponentIds.Contains(b.ComponentId))
                .Select(b => b.ProductId).Distinct();
            var revenueExposure = affectedProductIds
                .Where(products.ContainsKey)
                .Sum(pid => products[pid].DailyDemandUnits * products[pid].UnitRevenue);

            var probability = _riskScoring.CalculateSupplierRiskScore(supplier) / 100.0;

            cells.Add(new HeatmapCell(supplier.Name, "Supplier", Math.Round(probability, 3), 0, revenueExposure));
        }

        // Impact score is revenue exposure normalized against the highest
        // exposure in this organization's network, so the grid placement is
        // relative to this org's own scale rather than an arbitrary constant.
        var maxExposure = cells.Count == 0 ? 1 : Math.Max(1, cells.Max(c => c.RevenueExposureEstimate));
        var normalized = cells.Select(c => c with { ImpactScore = Math.Round((double)(c.RevenueExposureEstimate / maxExposure), 3) }).ToList();

        return View(normalized);
    }
}
