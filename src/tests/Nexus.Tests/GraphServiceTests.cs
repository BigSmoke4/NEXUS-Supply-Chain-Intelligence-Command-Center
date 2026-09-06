using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.SupplyNetwork;
using Xunit;

namespace Nexus.Tests;

public class GraphServiceTests
{
    [Fact]
    public async Task FindSinglePointsOfFailureAsync_DetectsSoleSupplier()
    {
        var (db, orgId) = TestDb.Create();

        // Supplier A is the ONLY path from raw supply to Product A.
        var supplierA = Node(orgId, NodeType.Supplier, "Supplier A");
        var component = Node(orgId, NodeType.Component, "Component X");
        var factory = Node(orgId, NodeType.Factory, "Factory 1");
        var product = Node(orgId, NodeType.Product, "Product A");

        db.SupplyNodes.AddRange(supplierA, component, factory, product);
        db.SupplyEdges.AddRange(
            Edge(orgId, supplierA, component, EdgeType.Supplies),
            Edge(orgId, component, factory, EdgeType.Contains),
            Edge(orgId, factory, product, EdgeType.Produces));
        await db.SaveChangesAsync();

        var graph = new GraphService(db);
        var spofs = await graph.FindSinglePointsOfFailureAsync(orgId);

        Assert.Contains(spofs, n => n.Id == supplierA.Id);
    }

    [Fact]
    public async Task FindSinglePointsOfFailureAsync_DoesNotFlagRedundantSupplier()
    {
        var (db, orgId) = TestDb.Create();

        // Two independent suppliers both feed the same component, so neither
        // is a single point of failure for the product downstream of it.
        var supplierA = Node(orgId, NodeType.Supplier, "Supplier A");
        var supplierB = Node(orgId, NodeType.Supplier, "Supplier B");
        var component = Node(orgId, NodeType.Component, "Component X");
        var factory = Node(orgId, NodeType.Factory, "Factory 1");
        var product = Node(orgId, NodeType.Product, "Product A");

        db.SupplyNodes.AddRange(supplierA, supplierB, component, factory, product);
        db.SupplyEdges.AddRange(
            Edge(orgId, supplierA, component, EdgeType.Supplies),
            Edge(orgId, supplierB, component, EdgeType.Supplies),
            Edge(orgId, component, factory, EdgeType.Contains),
            Edge(orgId, factory, product, EdgeType.Produces));
        await db.SaveChangesAsync();

        var graph = new GraphService(db);
        var spofs = await graph.FindSinglePointsOfFailureAsync(orgId);

        Assert.DoesNotContain(spofs, n => n.Id == supplierA.Id);
        Assert.DoesNotContain(spofs, n => n.Id == supplierB.Id);
    }

    [Fact]
    public async Task GetDownstreamAsync_ReturnsAllReachableNodes()
    {
        var (db, orgId) = TestDb.Create();

        var a = Node(orgId, NodeType.Supplier, "A");
        var b = Node(orgId, NodeType.Component, "B");
        var c = Node(orgId, NodeType.Factory, "C");

        db.SupplyNodes.AddRange(a, b, c);
        db.SupplyEdges.AddRange(
            Edge(orgId, a, b, EdgeType.Supplies),
            Edge(orgId, b, c, EdgeType.Contains));
        await db.SaveChangesAsync();

        var graph = new GraphService(db);
        var downstream = await graph.GetDownstreamAsync(a.Id);

        Assert.Equal(2, downstream.Count);
        Assert.Contains(downstream, n => n.Id == b.Id);
        Assert.Contains(downstream, n => n.Id == c.Id);
    }

    private static SupplyNode Node(Guid orgId, NodeType type, string name) =>
        new() { OrganizationId = orgId, Type = type, NodeRefId = Guid.NewGuid(), Name = name };

    private static SupplyEdge Edge(Guid orgId, SupplyNode source, SupplyNode target, EdgeType type) =>
        new() { OrganizationId = orgId, SourceNodeId = source.Id, TargetNodeId = target.Id, Type = type };
}
