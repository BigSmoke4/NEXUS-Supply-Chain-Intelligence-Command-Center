using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Knowledge;
using Nexus.Web.Modules.Risk;
using Nexus.Web.Modules.SupplyNetwork;

namespace Nexus.Web.Modules.AI.Agents;

/// <summary>
/// Common shape for a specialist agent: given the baseline simulation result,
/// contribute Evidence from its own domain. Each agent only ever reads
/// deterministic data (DB, simulation output, graph) - none of them call the
/// LLM. This is what lets AgentOrchestrator fan them out with Task.WhenAll
/// (§16's "parallel agent execution") instead of running one monolithic
/// prompt against the whole database.
/// </summary>
public interface ISpecialistAgent
{
    string Name { get; }
    Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct);
}

/// Inventory Agent (§15): stockout risk and coverage days.
public class InventoryAgent : ISpecialistAgent
{
    public string Name => "Inventory Agent";
    private readonly NexusDbContext _db;
    public InventoryAgent(NexusDbContext db) => _db = db;

    public Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        var evidence = new List<Evidence>();
        var criticalStockouts = simulationResult.StockoutEvents.OrderBy(s => s.StockoutDayOffset).Take(3);
        foreach (var so in criticalStockouts)
        {
            evidence.Add(new Evidence(Name,
                $"Component stockout projected at day {so.StockoutDayOffset} for warehouse {so.WarehouseId}", 0.9));
        }
        if (!simulationResult.StockoutEvents.Any())
            evidence.Add(new Evidence(Name, "No stockouts projected within the simulation horizon.", 0.9));
        return Task.FromResult(evidence);
    }
}

/// Supplier Risk Agent (§15): composite risk of the affected supplier(s).
public class SupplierRiskAgent : ISpecialistAgent
{
    public string Name => "Supplier Risk Agent";
    private readonly NexusDbContext _db;
    private readonly IRiskScoringService _risk;
    public SupplierRiskAgent(NexusDbContext db, IRiskScoringService risk) { _db = db; _risk = risk; }

    public async Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        var suppliers = _db.Suppliers.Where(s => s.OrganizationId == organizationId).ToList();
        var evidence = new List<Evidence>();
        foreach (var s in suppliers.OrderByDescending(s => _risk.CalculateSupplierRiskScore(s)).Take(2))
        {
            var score = _risk.CalculateSupplierRiskScore(s);
            evidence.Add(new Evidence(Name, $"{s.Name} composite risk score: {score}/100", 0.85));
        }
        return await Task.FromResult(evidence);
    }
}

/// Transportation Agent (§15): flags routes/lanes implicated by the disruption.
public class TransportationAgent : ISpecialistAgent
{
    public string Name => "Transportation Agent";

    public Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        // A full implementation would traverse TransportedVia edges and cross
        // reference active Disruptions of type TransportationFailure/PortClosure.
        // Kept intentionally minimal here rather than fabricating route-level
        // detail the seed data doesn't yet model.
        var evidence = new List<Evidence>
        {
            new(Name, "No transportation-route-level data modeled in this deployment; " +
                      "extend SupplyEdge.Type == TransportedVia traversal to add lane-level evidence.", 0.5)
        };
        return Task.FromResult(evidence);
    }
}

/// Financial Impact Agent (§15): restates revenue exposure with confidence framing.
public class FinancialImpactAgent : ISpecialistAgent
{
    public string Name => "Financial Impact Agent";

    public Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        var evidence = new List<Evidence>
        {
            new(Name, $"Revenue at risk from the deterministic simulation: ${simulationResult.RevenueAtRisk:N0}", 0.95),
            new(Name, $"Projected service level during the disruption: {simulationResult.ServiceLevelPercent}%", 0.9)
        };
        return Task.FromResult(evidence);
    }
}

/// Graph Intelligence Agent (§15): traverses the dependency graph to surface
/// single-point-of-failure exposure connected to the disrupted node.
public class GraphIntelligenceAgent : ISpecialistAgent
{
    public string Name => "Graph Intelligence Agent";
    private readonly IGraphService _graph;
    public GraphIntelligenceAgent(IGraphService graph) => _graph = graph;

    public async Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        var spofs = await _graph.FindSinglePointsOfFailureAsync(organizationId, ct);
        var evidence = new List<Evidence>();

        if (spofs.Any())
        {
            evidence.Add(new Evidence(Name,
                $"{spofs.Count} single point(s) of failure identified in the network: {string.Join(", ", spofs.Select(n => n.Name).Take(5))}",
                0.85));
        }
        else
        {
            evidence.Add(new Evidence(Name, "No single points of failure detected in the current network topology.", 0.85));
        }

        return evidence;
    }
}

/// Contract Retrieval Agent (§42): grounds the answer in ingested enterprise
/// documents (contracts, procurement policy, transportation agreements) via
/// lexical retrieval, and cites the source document for every fact it
/// contributes. Retrieved text is placed into Evidence as a quoted fact only
/// - it is never treated as an instruction (§44); AgentOrchestrator's system
/// prompt explicitly tells the LLM to treat all evidence, including this
/// agent's, as data to explain, not commands to follow.
public class ContractRetrievalAgent : ISpecialistAgent
{
    public string Name => "Contract Retrieval Agent";
    private readonly IDocumentRetrievalService _retrieval;
    public ContractRetrievalAgent(IDocumentRetrievalService retrieval) => _retrieval = retrieval;

    public async Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        var passages = await _retrieval.RetrieveAsync(organizationId, question, topK: 3, ct: ct);
        if (!passages.Any())
        {
            return new List<Evidence>
            {
                new(Name, "No relevant contract, policy, or agreement text was found for this question in the ingested document set.", 0.5)
            };
        }

        // Confidence scales with retrieval score but is deliberately capped
        // below the deterministic-simulation agents' confidence (§18):
        // lexical retrieval can surface a relevant-looking passage that
        // doesn't actually answer the question, so it should read as
        // supporting context, not as calculated fact.
        return passages
            .Select(p => new Evidence(Name, $"[{p.DocumentType}: {p.Title}] \"{p.Snippet}\"", Math.Min(0.75, 0.4 + p.Score)))
            .ToList();
    }
}

/// Research Agent (§15): retrieves external information ONLY when an
/// approved external data source is explicitly connected. No such connector
/// is wired up in this build (no outbound network access here), so this
/// agent honestly reports that instead of fabricating external context -
/// per §44, retrieved content must come from an identified, trusted source
/// or not be presented as retrieved at all.
public class ResearchAgent : ISpecialistAgent
{
    public string Name => "Research Agent";

    public Task<List<Evidence>> InvestigateAsync(Guid organizationId, ScenarioResult simulationResult, string question, CancellationToken ct)
    {
        var evidence = new List<Evidence>
        {
            new(Name, "No external data source (news feed, supplier portal, trade database) is connected in this " +
                      "deployment, so no external context is included. Wire an approved connector behind this " +
                      "agent to add it - never let unvetted retrieved content into the evidence list (§44).", 0.4)
        };
        return Task.FromResult(evidence);
    }
}
