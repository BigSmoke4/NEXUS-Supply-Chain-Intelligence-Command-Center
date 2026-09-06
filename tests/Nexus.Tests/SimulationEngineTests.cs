using Microsoft.EntityFrameworkCore;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Simulation;
using Nexus.Web.Modules.SupplyNetwork;
using Xunit;

namespace Nexus.Tests;

public class SimulationEngineTests
{
    [Fact]
    public async Task RunAsync_ComputesStockoutDay_FromInventoryAndConsumption()
    {
        var (db, orgId) = TestDb.Create();

        // Arrange: Supplier -[Supplies]-> Component, one warehouse holding
        // 1000 units of that component consumed at 100/day. A 100%-severity
        // disruption on the supplier should zero out inbound replenishment,
        // so the warehouse should hit zero exactly on day 9 (1000 - 100*10 = 0).
        var supplierNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Supplier, NodeRefId = Guid.NewGuid(), Name = "Supplier A" };
        var componentNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Component, NodeRefId = Guid.NewGuid(), Name = "Component X" };
        db.SupplyNodes.AddRange(supplierNode, componentNode);
        db.SupplyEdges.Add(new SupplyEdge
        {
            OrganizationId = orgId, SourceNodeId = supplierNode.Id, TargetNodeId = componentNode.Id, Type = EdgeType.Supplies
        });

        var component = new Component { OrganizationId = orgId, Id = componentNode.NodeRefId, Name = "Component X", Sku = "CMP-X" };
        db.Components.Add(component);

        var warehouse = new Warehouse { OrganizationId = orgId, Name = "Warehouse 1", Region = "Test" };
        db.Warehouses.Add(warehouse);
        db.InventoryRecords.Add(new InventoryRecord
        {
            OrganizationId = orgId, WarehouseId = warehouse.Id, ComponentId = component.Id,
            QuantityOnHand = 1000, SafetyStock = 100, DailyConsumption = 100
        });

        var product = new Product { OrganizationId = orgId, Name = "Product A", Sku = "PRD-A", UnitRevenue = 50, DailyDemandUnits = 20 };
        db.Products.Add(product);
        db.BillOfMaterials.Add(new BillOfMaterial { ProductId = product.Id, ComponentId = component.Id, QuantityPerUnit = 1 });

        var scenario = new Scenario { OrganizationId = orgId, Name = "Test scenario" };
        var start = DateTime.UtcNow.Date;
        scenario.Disruptions.Add(new Disruption
        {
            OrganizationId = orgId, Type = DisruptionType.SupplierShutdown, AffectedNodeId = supplierNode.Id,
            CapacityReductionPercent = 100, StartDateUtc = start, EndDateUtc = start.AddDays(30)
        });
        db.Scenarios.Add(scenario);
        await db.SaveChangesAsync();

        var graph = new GraphService(db);
        var engine = new SimulationEngine(db, graph);

        // Act
        var result = await engine.RunAsync(scenario.Id);

        // Assert
        var stockout = Assert.Single(result.StockoutEvents);
        Assert.Equal(9, stockout.StockoutDayOffset);

        // lostDays = recoveryDay(30) - stockoutDay(9) = 21
        // revenueAtRisk = 21 * dailyDemand(20) * unitRevenue(50) = 21000
        Assert.Equal(21000m, result.RevenueAtRisk);
        Assert.Equal(1, result.ProductsAffected);
    }

    [Fact]
    public async Task RunAsync_NoDisruptionsAffectingInventory_ProducesNoStockouts()
    {
        var (db, orgId) = TestDb.Create();

        // A disruption on a node with no downstream components should leave
        // every warehouse's inventory untouched.
        var isolatedNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Supplier, NodeRefId = Guid.NewGuid(), Name = "Isolated Supplier" };
        db.SupplyNodes.Add(isolatedNode);

        var component = new Component { OrganizationId = orgId, Name = "Component Y", Sku = "CMP-Y" };
        db.Components.Add(component);
        var warehouse = new Warehouse { OrganizationId = orgId, Name = "Warehouse 2", Region = "Test" };
        db.Warehouses.Add(warehouse);
        db.InventoryRecords.Add(new InventoryRecord
        {
            OrganizationId = orgId, WarehouseId = warehouse.Id, ComponentId = component.Id,
            QuantityOnHand = 500, SafetyStock = 50, DailyConsumption = 40
        });

        var scenario = new Scenario { OrganizationId = orgId, Name = "Isolated disruption" };
        var start = DateTime.UtcNow.Date;
        scenario.Disruptions.Add(new Disruption
        {
            OrganizationId = orgId, Type = DisruptionType.SupplierShutdown, AffectedNodeId = isolatedNode.Id,
            CapacityReductionPercent = 100, StartDateUtc = start, EndDateUtc = start.AddDays(10)
        });
        db.Scenarios.Add(scenario);
        await db.SaveChangesAsync();

        var engine = new SimulationEngine(db, new GraphService(db));

        var result = await engine.RunAsync(scenario.Id);

        Assert.Empty(result.StockoutEvents);
        Assert.Equal(0m, result.RevenueAtRisk);
    }

    [Fact]
    public async Task RunAsync_ComputesCascadeSteps_WithCorrectBfsStageOrder()
    {
        var (db, orgId) = TestDb.Create();

        // Supplier -> Component -> Factory -> Product: a 3-hop chain, so
        // CascadeSteps should show stage 0 (supplier), 1 (component),
        // 2 (factory), 3 (product) - exactly the BFS depth from the
        // disrupted node, not an arbitrary UI-invented sequence.
        var supplierNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Supplier, NodeRefId = Guid.NewGuid(), Name = "Supplier A" };
        var componentNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Component, NodeRefId = Guid.NewGuid(), Name = "Component X" };
        var factoryNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Factory, NodeRefId = Guid.NewGuid(), Name = "Factory 1" };
        var productNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Product, NodeRefId = Guid.NewGuid(), Name = "Product A" };
        db.SupplyNodes.AddRange(supplierNode, componentNode, factoryNode, productNode);
        db.SupplyEdges.AddRange(
            new SupplyEdge { OrganizationId = orgId, SourceNodeId = supplierNode.Id, TargetNodeId = componentNode.Id, Type = EdgeType.Supplies },
            new SupplyEdge { OrganizationId = orgId, SourceNodeId = componentNode.Id, TargetNodeId = factoryNode.Id, Type = EdgeType.Contains },
            new SupplyEdge { OrganizationId = orgId, SourceNodeId = factoryNode.Id, TargetNodeId = productNode.Id, Type = EdgeType.Produces });

        var component = new Component { OrganizationId = orgId, Id = componentNode.NodeRefId, Name = "Component X", Sku = "CMP-X" };
        db.Components.Add(component);
        var warehouse = new Warehouse { OrganizationId = orgId, Name = "Warehouse 1", Region = "Test" };
        db.Warehouses.Add(warehouse);
        db.InventoryRecords.Add(new InventoryRecord
        {
            OrganizationId = orgId, WarehouseId = warehouse.Id, ComponentId = component.Id,
            QuantityOnHand = 100, SafetyStock = 10, DailyConsumption = 100
        });

        var scenario = new Scenario { OrganizationId = orgId, Name = "Cascade test" };
        var start = DateTime.UtcNow.Date;
        scenario.Disruptions.Add(new Disruption
        {
            OrganizationId = orgId, Type = DisruptionType.SupplierShutdown, AffectedNodeId = supplierNode.Id,
            CapacityReductionPercent = 100, StartDateUtc = start, EndDateUtc = start.AddDays(10)
        });
        db.Scenarios.Add(scenario);
        await db.SaveChangesAsync();

        var engine = new SimulationEngine(db, new GraphService(db));
        var result = await engine.RunAsync(scenario.Id);

        Assert.Equal(4, result.CascadeSteps.Count);
        Assert.Equal(0, result.CascadeSteps.Single(c => c.NodeId == supplierNode.Id).StageOrder);
        Assert.Equal(1, result.CascadeSteps.Single(c => c.NodeId == componentNode.Id).StageOrder);
        Assert.Equal(2, result.CascadeSteps.Single(c => c.NodeId == factoryNode.Id).StageOrder);
        Assert.Equal(3, result.CascadeSteps.Single(c => c.NodeId == productNode.Id).StageOrder);
    }
}
