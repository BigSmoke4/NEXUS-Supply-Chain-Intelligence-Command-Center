using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.SupplyNetwork;

public interface IGraphService
{
    /// Every node reachable downstream from <paramref name="startNodeId"/>, following
    /// edge direction (Supplies -> Produces -> Ships -> ... -> SoldTo).
    Task<List<SupplyNode>> GetDownstreamAsync(Guid startNodeId, CancellationToken ct = default);

    /// Every node this node depends on upstream (suppliers of suppliers, etc.).
    Task<List<SupplyNode>> GetUpstreamAsync(Guid startNodeId, CancellationToken ct = default);

    /// Every node reachable downstream from <paramref name="startNodeId"/>, this time recording the number of graph-edge hops
    /// from the origin, so a caller (the simulation engine's cascade visualization) can group nodes into propagation stages.
    Task<List<(SupplyNode Node, int Depth)>> GetDownstreamWithDepthAsync(Guid startNodeId, CancellationToken ct = default);

    /// Nodes whose removal disconnects some downstream node from every upstream
    /// path that currently feeds it - i.e. true single points of failure.
    Task<List<SupplyNode>> FindSinglePointsOfFailureAsync(Guid organizationId, CancellationToken ct = default);

    /// Alternative paths from source to target that do NOT pass through
    /// <paramref name="excludeNodeId"/> - used to find substitute suppliers/routes.
    Task<List<List<SupplyNode>>> FindAlternatePathsAsync(Guid sourceNodeId, Guid targetNodeId, Guid excludeNodeId, CancellationToken ct = default);
}

public class GraphService : IGraphService
{
    private readonly NexusDbContext _db;
    public GraphService(NexusDbContext db) => _db = db;

    public async Task<List<SupplyNode>> GetDownstreamAsync(Guid startNodeId, CancellationToken ct = default)
    {
        var (nodes, edges) = await LoadGraphAsync(ct);
        return BreadthFirstTraverse(startNodeId, nodes, edges, e => e.SourceNodeId, e => e.TargetNodeId);
    }

    public async Task<List<(SupplyNode Node, int Depth)>> GetDownstreamWithDepthAsync(Guid startNodeId, CancellationToken ct = default)
    {
        var (nodes, edges) = await LoadGraphAsync(ct);
        var adjacency = edges.GroupBy(e => e.SourceNodeId).ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList());

        var visited = new HashSet<Guid> { startNodeId };
        var queue = new Queue<(Guid Id, int Depth)>();
        queue.Enqueue((startNodeId, 0));
        var result = new List<(SupplyNode, int)>();

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;

            foreach (var next in neighbors)
            {
                if (!visited.Add(next)) continue;
                if (nodes.TryGetValue(next, out var node)) result.Add((node, depth + 1));
                queue.Enqueue((next, depth + 1));
            }
        }
        return result;
    }

    public async Task<List<SupplyNode>> GetUpstreamAsync(Guid startNodeId, CancellationToken ct = default)
    {
        var (nodes, edges) = await LoadGraphAsync(ct);
        return BreadthFirstTraverse(startNodeId, nodes, edges, e => e.TargetNodeId, e => e.SourceNodeId);
    }

    private static List<SupplyNode> BreadthFirstTraverse(
        Guid startNodeId, Dictionary<Guid, SupplyNode> nodes, List<SupplyEdge> edges,
        Func<SupplyEdge, Guid> fromSelector, Func<SupplyEdge, Guid> toSelector)
    {
        var adjacency = edges.GroupBy(fromSelector)
            .ToDictionary(g => g.Key, g => g.Select(toSelector).ToList());

        var visited = new HashSet<Guid> { startNodeId };
        var queue = new Queue<Guid>();
        queue.Enqueue(startNodeId);
        var result = new List<SupplyNode>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;

            foreach (var next in neighbors)
            {
                if (!visited.Add(next)) continue;
                if (nodes.TryGetValue(next, out var node)) result.Add(node);
                queue.Enqueue(next);
            }
        }
        return result;
    }

    public async Task<List<SupplyNode>> FindSinglePointsOfFailureAsync(Guid organizationId, CancellationToken ct = default)
    {
        var (nodes, edges) = await LoadGraphAsync(ct, organizationId);
        var spofs = new List<SupplyNode>();

        // A node is a SPOF if some downstream node loses ALL of its upstream
        // paths when that node is removed. We test this by recomputing upstream
        // reachability for every "sink" (Product/Customer) node with the
        // candidate removed, and comparing against the baseline.
        var sinks = nodes.Values.Where(n => n.Type is NodeType.Product or NodeType.Customer).ToList();
        var baselineUpstream = sinks.ToDictionary(
            s => s.Id,
            s => new HashSet<Guid>(BreadthFirstTraverse(s.Id, nodes, edges, e => e.TargetNodeId, e => e.SourceNodeId).Select(n => n.Id)));

        foreach (var candidate in nodes.Values.Where(n => n.Type is NodeType.Supplier or NodeType.Factory or NodeType.Warehouse))
        {
            var edgesWithoutCandidate = edges
                .Where(e => e.SourceNodeId != candidate.Id && e.TargetNodeId != candidate.Id)
                .ToList();

            bool isSpof = false;
            foreach (var sink in sinks)
            {
                if (!baselineUpstream[sink.Id].Contains(candidate.Id)) continue; // sink didn't depend on it anyway

                var upstreamWithout = new HashSet<Guid>(
                    BreadthFirstTraverse(sink.Id, nodes, edgesWithoutCandidate, e => e.TargetNodeId, e => e.SourceNodeId)
                        .Select(n => n.Id));

                // If removing the candidate drops every supplier-type ancestor
                // the sink had, there is no remaining path to raw supply.
                var hadSupplierAncestor = baselineUpstream[sink.Id]
                    .Any(id => nodes.TryGetValue(id, out var n) && n.Type == NodeType.Supplier);
                var stillHasSupplierAncestor = upstreamWithout
                    .Any(id => nodes.TryGetValue(id, out var n) && n.Type == NodeType.Supplier);

                if (hadSupplierAncestor && !stillHasSupplierAncestor)
                {
                    isSpof = true;
                    break;
                }
            }

            if (isSpof) spofs.Add(candidate);
        }

        return spofs;
    }

    public async Task<List<List<SupplyNode>>> FindAlternatePathsAsync(Guid sourceNodeId, Guid targetNodeId, Guid excludeNodeId, CancellationToken ct = default)
    {
        var (nodes, edges) = await LoadGraphAsync(ct);
        var filtered = edges.Where(e => e.SourceNodeId != excludeNodeId && e.TargetNodeId != excludeNodeId).ToList();
        var adjacency = filtered.GroupBy(e => e.SourceNodeId).ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList());

        var paths = new List<List<Guid>>();
        var stack = new Stack<(Guid node, List<Guid> path)>();
        stack.Push((sourceNodeId, new List<Guid> { sourceNodeId }));

        while (stack.Count > 0 && paths.Count < 25) // cap depth-first search for performance
        {
            var (node, path) = stack.Pop();
            if (node == targetNodeId) { paths.Add(path); continue; }
            if (path.Count > 8) continue; // avoid pathological depth

            if (!adjacency.TryGetValue(node, out var neighbors)) continue;
            foreach (var n in neighbors.Where(n => !path.Contains(n)))
                stack.Push((n, new List<Guid>(path) { n }));
        }

        return paths.Select(p => p.Select(id => nodes[id]).ToList()).ToList();
    }

    private async Task<(Dictionary<Guid, SupplyNode> nodes, List<SupplyEdge> edges)> LoadGraphAsync(CancellationToken ct, Guid? organizationId = null)
    {
        var nodesQuery = _db.SupplyNodes.AsQueryable();
        var edgesQuery = _db.SupplyEdges.AsQueryable();
        if (organizationId is Guid orgId)
        {
            nodesQuery = nodesQuery.Where(n => n.OrganizationId == orgId);
            edgesQuery = edgesQuery.Where(e => e.OrganizationId == orgId);
        }

        var nodes = await Task.Run(() => nodesQuery.ToList(), ct);
        var edges = await Task.Run(() => edgesQuery.ToList(), ct);
        return (nodes.ToDictionary(n => n.Id), edges);
    }
}
