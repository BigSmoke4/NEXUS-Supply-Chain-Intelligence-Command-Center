using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.WhatIf;
using Xunit;

namespace Nexus.Tests;

public class WhatIfEngineTests
{
    [Fact]
    public async Task CalculateAsync_FullCapacityNoDisruption_ReturnsZeroExposure()
    {
        var (db, orgId) = TestDb.Create();
        var node = new SupplyNode { OrganizationId = orgId, Type = NodeType.Supplier, NodeRefId = Guid.NewGuid(), Name = "Supplier A" };
        db.SupplyNodes.Add(node);
        await db.SaveChangesAsync();

        var engine = new WhatIfEngine(db);
        var result = await engine.CalculateAsync(orgId, new WhatIfInput(node.Id, 100, 0, 100));

        // 100% capacity = no reduction fraction = zero revenue at risk,
        // regardless of what's downstream.
        Assert.Equal(0m, result.EstimatedRevenueAtRisk);
        Assert.Equal(100, result.EstimatedServiceLevelPercent);
    }

    [Fact]
    public async Task CalculateAsync_FullShutdown_ComputesRevenueExposureFromDownstreamProducts()
    {
        var (db, orgId) = TestDb.Create();

        var supplierNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Supplier, NodeRefId = Guid.NewGuid(), Name = "Supplier A" };
        var componentNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Component, NodeRefId = Guid.NewGuid(), Name = "Component X" };
        db.SupplyNodes.AddRange(supplierNode, componentNode);
        db.SupplyEdges.Add(new SupplyEdge { OrganizationId = orgId, SourceNodeId = supplierNode.Id, TargetNodeId = componentNode.Id, Type = EdgeType.Supplies });

        var component = new Component { OrganizationId = orgId, Id = componentNode.NodeRefId, Name = "Component X", Sku = "CMP-X" };
        db.Components.Add(component);

        var product = new Product { OrganizationId = orgId, Name = "Product A", Sku = "PRD-A", UnitRevenue = 100, DailyDemandUnits = 10 };
        db.Products.Add(product);
        db.BillOfMaterials.Add(new BillOfMaterial { ProductId = product.Id, ComponentId = component.Id, QuantityPerUnit = 1 });
        await db.SaveChangesAsync();

        var engine = new WhatIfEngine(db);

        // Full shutdown (0% capacity), 100% demand: revenue at risk =
        // dailyDemand(10) * unitRevenue(100) * reductionFraction(1) *
        // demandFactor(1) * 30-day exploration horizon = 30,000.
        var result = await engine.CalculateAsync(orgId, new WhatIfInput(supplierNode.Id, 0, 0, 100));

        Assert.Equal(30_000m, result.EstimatedRevenueAtRisk);
        Assert.Equal(1, result.AffectedProductCount);
    }

    [Fact]
    public async Task CalculateAsync_UnknownNode_ReturnsZeroResultRatherThanThrowing()
    {
        var (db, orgId) = TestDb.Create();
        var engine = new WhatIfEngine(db);

        var result = await engine.CalculateAsync(orgId, new WhatIfInput(Guid.NewGuid(), 50, 0, 100));

        Assert.Equal(0m, result.EstimatedRevenueAtRisk);
        Assert.Equal(0, result.AffectedProductCount);
    }

    [Fact]
    public async Task CalculateAsync_MoreAdditionalInventory_IncreasesEstimatedStockoutDays()
    {
        var (db, orgId) = TestDb.Create();

        var supplierNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Supplier, NodeRefId = Guid.NewGuid(), Name = "Supplier A" };
        var componentNode = new SupplyNode { OrganizationId = orgId, Type = NodeType.Component, NodeRefId = Guid.NewGuid(), Name = "Component X" };
        db.SupplyNodes.AddRange(supplierNode, componentNode);
        db.SupplyEdges.Add(new SupplyEdge { OrganizationId = orgId, SourceNodeId = supplierNode.Id, TargetNodeId = componentNode.Id, Type = EdgeType.Supplies });

        var component = new Component { OrganizationId = orgId, Id = componentNode.NodeRefId, Name = "Component X", Sku = "CMP-X" };
        db.Components.Add(component);

        var warehouse = new Warehouse { OrganizationId = orgId, Name = "Warehouse 1", Region = "Test" };
        db.Warehouses.Add(warehouse);
        db.InventoryRecords.Add(new InventoryRecord
        {
            OrganizationId = orgId, WarehouseId = warehouse.Id, ComponentId = component.Id,
            QuantityOnHand = 100, SafetyStock = 10, DailyConsumption = 10
        });
        await db.SaveChangesAsync();

        var engine = new WhatIfEngine(db);

        var withoutBuffer = await engine.CalculateAsync(orgId, new WhatIfInput(supplierNode.Id, 0, 0, 100));
        var withBuffer = await engine.CalculateAsync(orgId, new WhatIfInput(supplierNode.Id, 0, 20, 100));

        Assert.True(withBuffer.EstimatedStockoutDays > withoutBuffer.EstimatedStockoutDays);
    }
}
