using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.SupplyNetwork;

namespace Nexus.Web.Modules.Simulation;

/// <summary>
/// Deterministic, purely-arithmetic simulation of a disruption's propagation
/// through the supply graph. Per §10/§71: this is the sole source of truth
/// for numeric outcomes. The AI layer is only ever allowed to explain these
/// numbers - never invent or override them.
/// </summary>
public interface ISimulationEngine
{
    Task<ScenarioResult> RunAsync(Guid scenarioId, CancellationToken ct = default);
}

public class SimulationEngine : ISimulationEngine
{
    private const int HorizonDays = 120; // simulate far enough past any disruption to observe recovery

    private readonly NexusDbContext _db;
    private readonly IGraphService _graph;

    public SimulationEngine(NexusDbContext db, IGraphService graph)
    {
        _db = db;
        _graph = graph;
    }

    public async Task<ScenarioResult> RunAsync(Guid scenarioId, CancellationToken ct = default)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.Disruptions)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, ct)
            ?? throw new InvalidOperationException($"Scenario {scenarioId} not found.");

        var orgId = scenario.OrganizationId;
        var startDate = scenario.Disruptions.Min(d => d.StartDateUtc).Date;

        var suppliers = await _db.Suppliers.Where(s => s.OrganizationId == orgId).ToListAsync(ct);
        var factories = await _db.Factories.Where(f => f.OrganizationId == orgId).ToListAsync(ct);
        var warehouses = await _db.Warehouses.Where(w => w.OrganizationId == orgId).ToListAsync(ct);
        var components = await _db.Components.Where(c => c.OrganizationId == orgId).ToListAsync(ct);
        var products = await _db.Products.Where(p => p.OrganizationId == orgId).ToListAsync(ct);
        var customers = await _db.Customers.Where(c => c.OrganizationId == orgId).ToListAsync(ct);
        var boms = await _db.BillOfMaterials.ToListAsync(ct);
        var inventory = await _db.InventoryRecords.Where(i => i.OrganizationId == orgId).ToListAsync(ct);
        var edges = await _db.SupplyEdges.Where(e => e.OrganizationId == orgId).ToListAsync(ct);
        var nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == orgId).ToDictionaryAsync(n => n.Id, ct);

        var result = new ScenarioResult { OrganizationId = orgId, ScenarioId = scenario.Id };
        var timeline = new List<TimelineEvent>();

        // --- 1. Determine which supply-node capacity is reduced, per day ---
        // capacityFactor[nodeId][dayOffset] = fraction of normal capacity available (1.0 = normal)
        var capacityFactor = new Dictionary<Guid, double[]>();
        foreach (var d in scenario.Disruptions)
        {
            if (!capacityFactor.ContainsKey(d.AffectedNodeId))
                capacityFactor[d.AffectedNodeId] = Enumerable.Repeat(1.0, HorizonDays).ToArray();

            var arr = capacityFactor[d.AffectedNodeId];
            var startOffset = (int)(d.StartDateUtc.Date - startDate).TotalDays;
            var endOffset = (int)(d.EndDateUtc.Date - startDate).TotalDays;
            var remainingFactor = 1 - (d.CapacityReductionPercent / 100.0);

            for (int day = Math.Max(0, startOffset); day < Math.Min(HorizonDays, endOffset); day++)
                arr[day] = Math.Min(arr[day], remainingFactor);
        }

        if (scenario.Disruptions.Any())
        {
            timeline.Add(new TimelineEvent { OrganizationId = orgId, DayOffset = 0, Label = $"Disruption begins: {scenario.Disruptions.First().Type}", Severity = "critical" });
        }

        // --- 2. Propagate capacity loss downstream through the graph to affected components/warehouses ---
        var affectedComponentIds = new HashSet<Guid>();
        var cascadeSteps = new List<CascadeStep>();
        var cascadeVisited = new HashSet<Guid>();

        foreach (var disruptedNodeId in capacityFactor.Keys)
        {
            if (nodes.TryGetValue(disruptedNodeId, out var originNode) && cascadeVisited.Add(disruptedNodeId))
            {
                cascadeSteps.Add(new CascadeStep { OrganizationId = orgId, NodeId = disruptedNodeId, NodeName = originNode.Name, NodeType = originNode.Type.ToString(), StageOrder = 0 });
            }

            var downstreamWithDepth = await _graph.GetDownstreamWithDepthAsync(disruptedNodeId, ct);
            foreach (var (n, depth) in downstreamWithDepth)
            {
                if (n.Type == NodeType.Component) affectedComponentIds.Add(n.NodeRefId);
                if (cascadeVisited.Add(n.Id))
                    cascadeSteps.Add(new CascadeStep { OrganizationId = orgId, NodeId = n.Id, NodeName = n.Name, NodeType = n.Type.ToString(), StageOrder = depth });
            }
        }

        // --- 3. Day-by-day inventory depletion per (component, warehouse) - §8 ---
        // Undisrupted components are assumed to stay within safety stock via
        // normal replenishment and are skipped - only components downstream of
        // a disrupted node are walked day-by-day.
        var stockouts = new List<StockoutEvent>();
        var stockoutDayByComponent = new Dictionary<Guid, int>();

        foreach (var inv in inventory.Where(i => affectedComponentIds.Contains(i.ComponentId)))
        {
            decimal qty = inv.QuantityOnHand;

            for (int day = 0; day < HorizonDays; day++)
            {
                // Inbound replenishment is throttled by the worst capacity factor
                // affecting this component's upstream supply that day; during a
                // full (100%) shutdown that factor is 0, i.e. no inbound at all.
                var inboundFactor = capacityFactor.Values
                    .Select(arr => arr[day])
                    .DefaultIfEmpty(1.0)
                    .Min();

                var consumption = inv.DailyConsumption;
                var replenishment = (decimal)inboundFactor * consumption;
                qty = qty - consumption + replenishment;

                if (qty <= 0)
                {
                    stockouts.Add(new StockoutEvent
                    {
                        OrganizationId = orgId,
                        ComponentId = inv.ComponentId,
                        WarehouseId = inv.WarehouseId,
                        StockoutDayOffset = day
                    });
                    stockoutDayByComponent.TryAdd(inv.ComponentId, day);
                    break;
                }
            }
        }

        foreach (var so in stockouts.OrderBy(s => s.StockoutDayOffset))
        {
            var componentName = components.FirstOrDefault(c => c.Id == so.ComponentId)?.Name ?? "component";
            timeline.Add(new TimelineEvent
            {
                OrganizationId = orgId,
                DayOffset = so.StockoutDayOffset,
                Label = $"Stockout: {componentName}",
                Severity = "critical"
            });
        }

        // --- 4. Determine which products are affected via BOM, and compute revenue exposure ---
        var affectedProductIds = boms
            .Where(b => affectedComponentIds.Contains(b.ComponentId))
            .Select(b => b.ProductId)
            .Distinct()
            .ToHashSet();

        decimal revenueAtRisk = 0;
        foreach (var productId in affectedProductIds)
        {
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null) continue;

            // Days of lost production = horizon days from first stockout affecting
            // this product's BOM until recovery (end of the longest disruption).
            var relevantComponentIds = boms.Where(b => b.ProductId == productId).Select(b => b.ComponentId);
            var earliestStockout = relevantComponentIds
                .Where(stockoutDayByComponent.ContainsKey)
                .Select(cid => stockoutDayByComponent[cid])
                .DefaultIfEmpty(-1)
                .Min();

            if (earliestStockout < 0) continue;

            var recoveryDay = scenario.Disruptions.Any()
                ? (int)(scenario.Disruptions.Max(d => d.EndDateUtc.Date) - startDate).TotalDays
                : earliestStockout;

            var lostDays = Math.Max(0, recoveryDay - earliestStockout);
            revenueAtRisk += lostDays * product.DailyDemandUnits * product.UnitRevenue;
        }

        var affectedCustomerCount = customers.Count(c => c.Segment != null) > 0
            ? Math.Max(1, (int)Math.Round(customers.Count * Math.Min(1.0, affectedProductIds.Count / (double)Math.Max(1, products.Count))))
            : 0;

        var recoveryDays = scenario.Disruptions.Any()
            ? (int)(scenario.Disruptions.Max(d => d.EndDateUtc.Date) - startDate).TotalDays
              + EstimateRampUpDays(stockouts.Count)
            : 0;

        var serviceLevel = products.Count == 0
            ? 100.0
            : 100.0 * (1 - (affectedProductIds.Count / (double)products.Count) * 0.6); // partial service degradation, not full loss

        result.RevenueAtRisk = revenueAtRisk;
        result.ProductsAffected = affectedProductIds.Count;
        result.CustomersAffected = affectedCustomerCount;
        result.RecoveryDays = recoveryDays;
        result.ServiceLevelPercent = Math.Round(Math.Clamp(serviceLevel, 0, 100), 1);
        result.StockoutEvents = stockouts;
        result.Timeline = timeline.OrderBy(t => t.DayOffset).ToList();
        result.CascadeSteps = cascadeSteps.OrderBy(c => c.StageOrder).ToList();

        // Overall risk score: weighted blend of financial exposure severity and
        // service-level degradation, normalized 0-100 (see RiskScoringService for
        // the supplier-level composite score used elsewhere).
        var normalizedRevenue = products.Count == 0 ? 0 : Math.Min(1.0, (double)revenueAtRisk / 5_000_000.0);
        result.OverallRiskScore = Math.Round((normalizedRevenue * 0.6 + (1 - serviceLevel / 100.0) * 0.4) * 100, 1);

        return result;
    }

    private static int EstimateRampUpDays(int stockoutCount) => stockoutCount switch
    {
        0 => 0,
        <= 2 => 5,
        <= 5 => 10,
        _ => 18
    };
}
