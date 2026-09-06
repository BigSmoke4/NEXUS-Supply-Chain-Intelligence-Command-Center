using Microsoft.EntityFrameworkCore;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Security;

namespace Nexus.Web.Data;

/// <summary>
/// Realistic, interconnected demo network per §54, at close to the production
/// scale called for (25 suppliers / 40 components / 12 factories / 8
/// warehouses / 100 products / 20 customers). Deliberately includes:
///  - a single-supplier dependency (Supplier Alpha is the ONLY source of
///    Component X, which feeds two factories and three products)
///  - a low-inventory-coverage warehouse (Warehouse 1: ~9 days of cover)
///  - a high-risk supplier (Supplier Gamma: high geopolitical + operational risk)
/// so the pre-built demo scenarios (§55) produce a compelling, realistic story.
/// The three hand-authored suppliers/components/factories/warehouses/products/
/// customers above carry that story; SeedBulkNetwork below extends the same
/// graph with a much larger, randomly-but-deterministically generated network
/// (fixed RNG seed = 42) so every code path - graph traversal, SPOF detection,
/// simulation, risk scoring - gets exercised at realistic scale, not just on
/// the three curated entities.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(NexusDbContext db)
    {
        if (await db.Organizations.AnyAsync()) return; // idempotent

        var org = new Organization { Name = "Acme Global Manufacturing" };
        db.Organizations.Add(org);
        // Organization is the only tenant-independent bootstrap record. Persist it
        // first so the remainder of the seed can run under an explicit tenant.
        await db.SaveChangesAsync();
        using var tenantScope = HttpContextCurrentTenant.UseTenant(org.Id);

        // ---- Suppliers ----
        var supplierAlpha = NewSupplier(org.Id, "Supplier Alpha", "Taiwan",
            financial: 25, geo: 55, operational: 40, transport: 30, quality: 15, concentration: 85,
            reliability: 94, leadTime: 21, capacity: 12000);

        var supplierBeta = NewSupplier(org.Id, "Supplier Beta", "Vietnam",
            financial: 20, geo: 35, operational: 30, transport: 25, quality: 12, concentration: 40,
            reliability: 97, leadTime: 18, capacity: 8000);

        var supplierGamma = NewSupplier(org.Id, "Supplier Gamma", "Region X (elevated geopolitical risk)",
            financial: 45, geo: 80, operational: 65, transport: 60, quality: 30, concentration: 70,
            reliability: 88, leadTime: 30, capacity: 5000);

        db.Suppliers.AddRange(supplierAlpha, supplierBeta, supplierGamma);

        // ---- Components ----
        var componentX = NewComponent(org.Id, "Component X (power module)", "CMP-X");
        var componentY = NewComponent(org.Id, "Component Y (sensor array)", "CMP-Y");
        var componentZ = NewComponent(org.Id, "Component Z (housing)", "CMP-Z");
        db.Components.AddRange(componentX, componentY, componentZ);

        // ---- Factories / Warehouses ----
        var factory1 = NewFactory(org.Id, "Factory 1 - Shenzhen", "APAC", dailyCapacity: 900);
        var factory2 = NewFactory(org.Id, "Factory 2 - Monterrey", "LATAM", dailyCapacity: 600);
        db.Factories.AddRange(factory1, factory2);

        var warehouse1 = NewWarehouse(org.Id, "Warehouse 1 - Regional Hub", "APAC");
        var warehouse2 = NewWarehouse(org.Id, "Warehouse 2 - Distribution", "NA");
        db.Warehouses.AddRange(warehouse1, warehouse2);

        // ---- Products / Customers ----
        var productA = NewProduct(org.Id, "Product A - Flagship Device", "PRD-A", unitRevenue: 220, dailyDemand: 300);
        var productB = NewProduct(org.Id, "Product B - Compact Device", "PRD-B", unitRevenue: 140, dailyDemand: 250);
        var productC = NewProduct(org.Id, "Product C - Industrial Unit", "PRD-C", unitRevenue: 610, dailyDemand: 60);
        db.Products.AddRange(productA, productB, productC);

        var customer1 = NewCustomer(org.Id, "Northwind Retail", "HIGH_VALUE");
        var customer2 = NewCustomer(org.Id, "Global Distributors Inc", "STANDARD");
        var customer3 = NewCustomer(org.Id, "Regional Electronics Co", "STANDARD");
        db.Customers.AddRange(customer1, customer2, customer3);

        // ---- Bill of Materials: Component X feeds all three products (single point of concentration) ----
        db.BillOfMaterials.AddRange(
            new BillOfMaterial { OrganizationId = org.Id, ProductId = productA.Id, ComponentId = componentX.Id, QuantityPerUnit = 1 },
            new BillOfMaterial { OrganizationId = org.Id, ProductId = productA.Id, ComponentId = componentY.Id, QuantityPerUnit = 2 },
            new BillOfMaterial { OrganizationId = org.Id, ProductId = productB.Id, ComponentId = componentX.Id, QuantityPerUnit = 1 },
            new BillOfMaterial { OrganizationId = org.Id, ProductId = productC.Id, ComponentId = componentX.Id, QuantityPerUnit = 3 },
            new BillOfMaterial { OrganizationId = org.Id, ProductId = productC.Id, ComponentId = componentZ.Id, QuantityPerUnit = 1 }
        );

        // ---- Graph nodes (one per business entity) ----
        var nAlpha = NewNode(org.Id, NodeType.Supplier, supplierAlpha.Id, supplierAlpha.Name);
        var nBeta = NewNode(org.Id, NodeType.Supplier, supplierBeta.Id, supplierBeta.Name);
        var nGamma = NewNode(org.Id, NodeType.Supplier, supplierGamma.Id, supplierGamma.Name);
        var nCompX = NewNode(org.Id, NodeType.Component, componentX.Id, componentX.Name);
        var nCompY = NewNode(org.Id, NodeType.Component, componentY.Id, componentY.Name);
        var nCompZ = NewNode(org.Id, NodeType.Component, componentZ.Id, componentZ.Name);
        var nFactory1 = NewNode(org.Id, NodeType.Factory, factory1.Id, factory1.Name);
        var nFactory2 = NewNode(org.Id, NodeType.Factory, factory2.Id, factory2.Name);
        var nWarehouse1 = NewNode(org.Id, NodeType.Warehouse, warehouse1.Id, warehouse1.Name);
        var nWarehouse2 = NewNode(org.Id, NodeType.Warehouse, warehouse2.Id, warehouse2.Name);
        var nProductA = NewNode(org.Id, NodeType.Product, productA.Id, productA.Name);
        var nProductB = NewNode(org.Id, NodeType.Product, productB.Id, productB.Name);
        var nProductC = NewNode(org.Id, NodeType.Product, productC.Id, productC.Name);
        var nCustomer1 = NewNode(org.Id, NodeType.Customer, customer1.Id, customer1.Name);
        var nCustomer2 = NewNode(org.Id, NodeType.Customer, customer2.Id, customer2.Name);
        var nCustomer3 = NewNode(org.Id, NodeType.Customer, customer3.Id, customer3.Name);

        db.SupplyNodes.AddRange(nAlpha, nBeta, nGamma, nCompX, nCompY, nCompZ, nFactory1, nFactory2,
            nWarehouse1, nWarehouse2, nProductA, nProductB, nProductC, nCustomer1, nCustomer2, nCustomer3);

        // ---- Edges: Supplier Alpha is the SOLE source of Component X (intentional SPOF) ----
        db.SupplyEdges.AddRange(
            Edge(org.Id, nAlpha, nCompX, EdgeType.Supplies, leadTime: 21, capacity: 12000, cost: 14, reliability: 94, moq: 500, priority: "HIGH"),
            Edge(org.Id, nBeta, nCompY, EdgeType.Supplies, leadTime: 18, capacity: 8000, cost: 9, reliability: 97, moq: 300, priority: "MEDIUM"),
            Edge(org.Id, nGamma, nCompZ, EdgeType.Supplies, leadTime: 30, capacity: 5000, cost: 6, reliability: 88, moq: 200, priority: "LOW"),

            Edge(org.Id, nCompX, nFactory1, EdgeType.Contains),
            Edge(org.Id, nCompY, nFactory1, EdgeType.Contains),
            Edge(org.Id, nCompX, nFactory2, EdgeType.Contains),
            Edge(org.Id, nCompZ, nFactory2, EdgeType.Contains),

            Edge(org.Id, nFactory1, nProductA, EdgeType.Produces),
            Edge(org.Id, nFactory1, nProductB, EdgeType.Produces),
            Edge(org.Id, nFactory2, nProductC, EdgeType.Produces),

            Edge(org.Id, nProductA, nWarehouse1, EdgeType.Stores),
            Edge(org.Id, nProductB, nWarehouse1, EdgeType.Stores),
            Edge(org.Id, nProductC, nWarehouse2, EdgeType.Stores),

            Edge(org.Id, nWarehouse1, nCustomer1, EdgeType.ShipsTo),
            Edge(org.Id, nWarehouse1, nCustomer2, EdgeType.ShipsTo),
            Edge(org.Id, nWarehouse2, nCustomer3, EdgeType.ShipsTo)
        );

        // ---- Inventory: Warehouse 1 deliberately has low coverage (~9 days) ----
        db.InventoryRecords.AddRange(
            new InventoryRecord { OrganizationId = org.Id, WarehouseId = warehouse1.Id, ComponentId = componentX.Id, QuantityOnHand = 5400, SafetyStock = 1000, DailyConsumption = 600 },
            new InventoryRecord { OrganizationId = org.Id, WarehouseId = warehouse1.Id, ComponentId = componentY.Id, QuantityOnHand = 9000, SafetyStock = 1500, DailyConsumption = 500 },
            new InventoryRecord { OrganizationId = org.Id, WarehouseId = warehouse2.Id, ComponentId = componentX.Id, QuantityOnHand = 3600, SafetyStock = 800, DailyConsumption = 400 },
            new InventoryRecord { OrganizationId = org.Id, WarehouseId = warehouse2.Id, ComponentId = componentZ.Id, QuantityOnHand = 4200, SafetyStock = 900, DailyConsumption = 300 }
        );

        // ---- Bulk-generated network extension, bringing the seed up toward
        // the production demo scale called for in §54 (25 suppliers / 40
        // components / 12 factories / 8 warehouses / 100 products / 20
        // customers) while keeping the hand-authored vulnerability story
        // above (Supplier Alpha's single-source dependency, Warehouse 1's
        // thin coverage, Supplier Gamma's risk profile) as the centerpiece
        // the demo scenarios are built around. Uses a fixed seed so the
        // network is identical across runs/environments. ----
        SeedBulkNetwork(db, org.Id,
            existingWarehouses: new[] { warehouse1, warehouse2 },
            existingFactoryNodes: new[] { nFactory1, nFactory2 },
            existingWarehouseNodes: new[] { nWarehouse1, nWarehouse2 });

        SeedKnowledgeDocuments(db, org.Id, supplierAlpha.Id, supplierBeta.Id);

        await db.SaveChangesAsync();
    }

    private static void SeedBulkNetwork(
        NexusDbContext db, Guid orgId,
        Warehouse[] existingWarehouses,
        SupplyNode[] existingFactoryNodes, SupplyNode[] existingWarehouseNodes)
    {
        var rng = new Random(42); // fixed seed: deterministic across environments

        const int additionalSuppliers = 22;   // total suppliers -> 25
        const int additionalComponents = 37;  // total components -> 40
        const int additionalFactories = 10;   // total factories -> 12
        const int additionalWarehouses = 6;   // total warehouses -> 8
        const int additionalProducts = 97;    // total products -> 100
        const int additionalCustomers = 17;   // total customers -> 20

        var countries = new[] { "China", "Mexico", "Germany", "India", "Vietnam", "Poland", "Brazil", "South Korea", "USA", "Malaysia" };
        var regions = new[] { "APAC", "EMEA", "LATAM", "NA" };
        var segments = new[] { "HIGH_VALUE", "STANDARD", "STANDARD", "EMERGING" };

        var suppliers = new List<Supplier>();
        var supplierNodes = new List<SupplyNode>();
        for (int i = 0; i < additionalSuppliers; i++)
        {
            var s = NewSupplier(orgId, $"Supplier {SupplierCode(i)}", countries[rng.Next(countries.Length)],
                financial: rng.Next(10, 60), geo: rng.Next(10, 70), operational: rng.Next(10, 60),
                transport: rng.Next(10, 55), quality: rng.Next(5, 40), concentration: rng.Next(15, 60),
                reliability: rng.Next(85, 99), leadTime: rng.Next(10, 35), capacity: rng.Next(2000, 15000));
            suppliers.Add(s);
            supplierNodes.Add(NewNode(orgId, NodeType.Supplier, s.Id, s.Name));
        }
        db.Suppliers.AddRange(suppliers);
        db.SupplyNodes.AddRange(supplierNodes);

        var components = new List<Component>();
        var componentNodes = new List<SupplyNode>();
        for (int i = 0; i < additionalComponents; i++)
        {
            var c = NewComponent(orgId, $"Component {ComponentCode(i)}", $"CMP-{ComponentCode(i)}");
            components.Add(c);
            componentNodes.Add(NewNode(orgId, NodeType.Component, c.Id, c.Name));
        }
        db.Components.AddRange(components);
        db.SupplyNodes.AddRange(componentNodes);

        // Each generated component gets 1-2 suppliers (never zero - an
        // orphaned component would be an unreachable, meaningless node).
        var edges = new List<SupplyEdge>();
        for (int i = 0; i < components.Count; i++)
        {
            var supplierCount = rng.Next(1, 3);
            var chosen = Enumerable.Range(0, supplierCount).Select(_ => rng.Next(suppliers.Count)).Distinct();
            foreach (var sIdx in chosen)
            {
                edges.Add(Edge(orgId, supplierNodes[sIdx], componentNodes[i], EdgeType.Supplies,
                    leadTime: rng.Next(10, 35), capacity: rng.Next(2000, 12000), cost: rng.Next(3, 40),
                    reliability: rng.Next(85, 99), moq: rng.Next(100, 1000),
                    priority: new[] { "LOW", "MEDIUM", "HIGH" }[rng.Next(3)]));
            }
        }

        var factories = new List<Factory>();
        var factoryNodes = new List<SupplyNode>();
        for (int i = 0; i < additionalFactories; i++)
        {
            var f = NewFactory(orgId, $"Factory {i + 3} - {regions[rng.Next(regions.Length)]}",
                regions[rng.Next(regions.Length)], rng.Next(300, 1200));
            factories.Add(f);
            factoryNodes.Add(NewNode(orgId, NodeType.Factory, f.Id, f.Name));
        }
        db.Factories.AddRange(factories);
        db.SupplyNodes.AddRange(factoryNodes);

        var allFactoryNodes = existingFactoryNodes.Concat(factoryNodes).ToList();

        // Each generated component feeds 1-2 factories.
        foreach (var compNode in componentNodes)
        {
            var factoryCount = rng.Next(1, 3);
            var chosen = Enumerable.Range(0, factoryCount).Select(_ => rng.Next(allFactoryNodes.Count)).Distinct();
            foreach (var fIdx in chosen)
                edges.Add(Edge(orgId, compNode, allFactoryNodes[fIdx], EdgeType.Contains));
        }

        var warehouses = new List<Warehouse>();
        var warehouseNodes = new List<SupplyNode>();
        for (int i = 0; i < additionalWarehouses; i++)
        {
            var w = NewWarehouse(orgId, $"Warehouse {i + 3} - {regions[rng.Next(regions.Length)]}", regions[rng.Next(regions.Length)]);
            warehouses.Add(w);
            warehouseNodes.Add(NewNode(orgId, NodeType.Warehouse, w.Id, w.Name));
        }
        db.Warehouses.AddRange(warehouses);
        db.SupplyNodes.AddRange(warehouseNodes);

        var allWarehouses = existingWarehouses.Concat(warehouses).ToList();
        var allWarehouseNodes = existingWarehouseNodes.Concat(warehouseNodes).ToList();

        var products = new List<Product>();
        var productNodes = new List<SupplyNode>();
        var boms = new List<BillOfMaterial>();
        for (int i = 0; i < additionalProducts; i++)
        {
            var p = NewProduct(orgId, $"Product {ProductCode(i)}", $"PRD-{ProductCode(i)}",
                unitRevenue: rng.Next(30, 800), dailyDemand: rng.Next(10, 400));
            products.Add(p);
            var pNode = NewNode(orgId, NodeType.Product, p.Id, p.Name);
            productNodes.Add(pNode);

            // Each product is produced by one factory and consumes 1-3 components.
            var factoryIdx = rng.Next(allFactoryNodes.Count);
            edges.Add(Edge(orgId, allFactoryNodes[factoryIdx], pNode, EdgeType.Produces));

            var bomComponentCount = rng.Next(1, 4);
            // Select distinct components because (OrganizationId, ProductId,
            // ComponentId) is intentionally unique in the relational model.
            // The earlier independent random picks could occasionally create
            // a duplicate BOM row and make deterministic seeding fail.
            foreach (var comp in components.OrderBy(_ => rng.Next()).Take(bomComponentCount))
            {
                boms.Add(new BillOfMaterial { OrganizationId = orgId, ProductId = p.Id, ComponentId = comp.Id, QuantityPerUnit = rng.Next(1, 4) });
            }

            var warehouseIdx = rng.Next(allWarehouseNodes.Count);
            edges.Add(Edge(orgId, pNode, allWarehouseNodes[warehouseIdx], EdgeType.Stores));
        }
        db.Products.AddRange(products);
        db.SupplyNodes.AddRange(productNodes);
        db.BillOfMaterials.AddRange(boms);

        var customers = new List<Customer>();
        var customerNodes = new List<SupplyNode>();
        for (int i = 0; i < additionalCustomers; i++)
        {
            var c = NewCustomer(orgId, $"Customer Account {i + 4}", segments[rng.Next(segments.Length)]);
            customers.Add(c);
            customerNodes.Add(NewNode(orgId, NodeType.Customer, c.Id, c.Name));
        }
        db.Customers.AddRange(customers);
        db.SupplyNodes.AddRange(customerNodes);

        foreach (var custNode in customerNodes)
        {
            var warehouseIdx = rng.Next(allWarehouseNodes.Count);
            edges.Add(Edge(orgId, allWarehouseNodes[warehouseIdx], custNode, EdgeType.ShipsTo));
        }

        db.SupplyEdges.AddRange(edges);

        // Give every generated component a baseline inventory record in a
        // random warehouse so the simulation engine has something to deplete
        // for any node an operator selects in the scenario builder, not just
        // the three hand-authored vulnerabilities above.
        var inventoryRecords = new List<InventoryRecord>();
        foreach (var comp in components)
        {
            var warehouseIdx = rng.Next(allWarehouseNodes.Count);
            var warehouse = allWarehouses[warehouseIdx];
            var dailyConsumption = rng.Next(50, 500);
            inventoryRecords.Add(new InventoryRecord
            {
                OrganizationId = orgId,
                WarehouseId = warehouse.Id,
                ComponentId = comp.Id,
                QuantityOnHand = dailyConsumption * rng.Next(8, 45), // 8-45 days of coverage
                SafetyStock = dailyConsumption * 3,
                DailyConsumption = dailyConsumption
            });
        }
        db.InventoryRecords.AddRange(inventoryRecords);
    }

    /// <summary>
    /// Seeds a handful of realistic contract/policy documents so the
    /// Contract Retrieval Agent (§42) has something real to ground answers
    /// in. In particular, this answers the exact example question from the
    /// original brief - "Can we switch from Supplier Alpha to Supplier Beta
    /// according to the contract?" - with an actual clause, not a fabricated
    /// answer.
    /// </summary>
    private static void SeedKnowledgeDocuments(NexusDbContext db, Guid orgId, Guid supplierAlphaId, Guid supplierBetaId)
    {
        db.KnowledgeDocuments.AddRange(
            new KnowledgeDocument
            {
                OrganizationId = orgId,
                Title = "Supplier Alpha Master Supply Agreement",
                DocumentType = "Contract",
                RelatedSupplierId = supplierAlphaId,
                Content =
                    "This Master Supply Agreement is entered into between Acme Global Manufacturing and Supplier Alpha " +
                    "for the supply of Component X. Section 4.2 Exclusivity: Buyer agrees to source no less than " +
                    "70 percent of its Component X volume from Supplier Alpha during the initial three year term. " +
                    "Section 4.3 Force Majeure Reallocation: Notwithstanding Section 4.2, in the event of a supply " +
                    "disruption at Supplier Alpha lasting more than fourteen consecutive days, Buyer may reallocate " +
                    "affected volume to a qualified alternative supplier without breaching the exclusivity commitment, " +
                    "provided Buyer resumes the minimum 70 percent allocation to Supplier Alpha within 60 days of " +
                    "the disruption ending. Section 6.1 Termination for Convenience: Either party may terminate this " +
                    "Agreement with 90 days written notice. Section 8.4 Minimum Order Quantity: 500 units per order, " +
                    "consistent with the terms recorded against the Supplier Alpha to Component X relationship."
            },
            new KnowledgeDocument
            {
                OrganizationId = orgId,
                Title = "Supplier Beta Master Supply Agreement",
                DocumentType = "Contract",
                RelatedSupplierId = supplierBetaId,
                Content =
                    "This Master Supply Agreement is entered into between Acme Global Manufacturing and Supplier Beta " +
                    "for the supply of Component Y. Section 3.1 Capacity Commitment: Supplier Beta commits to reserve " +
                    "up to 8000 units per month of manufacturing capacity for Buyer, available on 18 days lead time. " +
                    "Section 3.5 Expansion Rights: Buyer may request Supplier Beta qualify additional component lines, " +
                    "including Component X, subject to a qualification period of 45 to 60 days and a new pricing " +
                    "schedule. Section 6.1 Termination for Convenience: Either party may terminate this Agreement " +
                    "with 60 days written notice."
            },
            new KnowledgeDocument
            {
                OrganizationId = orgId,
                Title = "Global Procurement Risk Policy",
                DocumentType = "Policy",
                Content =
                    "Procurement Policy Section 2: Single-Source Risk. No component classified as Tier 1 critical may " +
                    "be sourced from a single supplier representing more than 80 percent of volume without an " +
                    "approved risk exception on file with the Risk Manager. Section 5: Disruption Response Authority. " +
                    "During an active disruption of 14 days or longer, the Operations Manager is authorized to " +
                    "approve emergency reallocation of up to 40 percent of affected volume to a pre-qualified " +
                    "alternate supplier without additional executive sign-off, provided the reallocation is logged " +
                    "in the audit system within 24 hours."
            },
            new KnowledgeDocument
            {
                OrganizationId = orgId,
                Title = "Standard Transportation Services Agreement",
                DocumentType = "TransportationAgreement",
                Content =
                    "This Transportation Services Agreement governs ocean and air freight lanes between APAC supplier " +
                    "sites and Acme Global Manufacturing distribution centers. Section 2.3 Expedite Rights: Buyer may " +
                    "convert any ocean freight lane to air freight on 48 hours notice, subject to a surcharge of " +
                    "approximately 4 to 6 times the standard ocean rate for the affected shipment. Section 5.1 " +
                    "Force Majeure: Neither party is liable for delay caused by port closure, natural disaster, or " +
                    "government action, but the carrier must propose an alternate routing within 5 business days."
            }
        );
    }

    private static string SupplierCode(int i) => ((char)('D' + i)).ToString(); // D..Y, avoiding A/B/C already used above
    private static string ComponentCode(int i) => $"{(char)('A' + i % 26)}{i / 26 + 1}";
    private static string ProductCode(int i) => $"{(i + 4):D3}";

    private static Supplier NewSupplier(Guid orgId, string name, string country,
        double financial, double geo, double operational, double transport, double quality, double concentration,
        double reliability, int leadTime, decimal capacity) => new()
    {
        OrganizationId = orgId, Name = name, Country = country,
        FinancialRisk = financial, GeopoliticalRisk = geo, OperationalRisk = operational,
        TransportationRisk = transport, QualityRisk = quality, ConcentrationRisk = concentration,
        ReliabilityPercent = reliability, DefaultLeadTimeDays = leadTime,
        MonthlyCapacity = capacity, AvailableCapacity = capacity
    };

    private static Component NewComponent(Guid orgId, string name, string sku) =>
        new() { OrganizationId = orgId, Name = name, Sku = sku };

    private static Factory NewFactory(Guid orgId, string name, string region, decimal dailyCapacity) =>
        new() { OrganizationId = orgId, Name = name, Region = region, DailyProductionCapacityUnits = dailyCapacity };

    private static Warehouse NewWarehouse(Guid orgId, string name, string region) =>
        new() { OrganizationId = orgId, Name = name, Region = region };

    private static Product NewProduct(Guid orgId, string name, string sku, decimal unitRevenue, decimal dailyDemand) =>
        new() { OrganizationId = orgId, Name = name, Sku = sku, UnitRevenue = unitRevenue, DailyDemandUnits = dailyDemand };

    private static Customer NewCustomer(Guid orgId, string name, string segment) =>
        new() { OrganizationId = orgId, Name = name, Segment = segment };

    private static SupplyNode NewNode(Guid orgId, NodeType type, Guid refId, string name) =>
        new() { OrganizationId = orgId, Type = type, NodeRefId = refId, Name = name };

    private static SupplyEdge Edge(Guid orgId, SupplyNode source, SupplyNode target, EdgeType type,
        int? leadTime = null, decimal? capacity = null, decimal? cost = null, double? reliability = null,
        int? moq = null, string? priority = null) => new()
    {
        OrganizationId = orgId, SourceNodeId = source.Id, TargetNodeId = target.Id, Type = type,
        LeadTimeDays = leadTime, CapacityPerMonth = capacity, UnitCost = cost,
        ReliabilityPercent = reliability, MinimumOrderQuantity = moq, ContractPriority = priority
    };
}
