namespace Nexus.Web.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SupplyNodeId { get; set; }
    public string Name { get; set; } = default!;
    public string Country { get; set; } = default!;

    public double FinancialRisk { get; set; }
    public double GeopoliticalRisk { get; set; }
    public double OperationalRisk { get; set; }
    public double TransportationRisk { get; set; }
    public double QualityRisk { get; set; }
    public double ConcentrationRisk { get; set; }

    public double ReliabilityPercent { get; set; } = 95;
    public int DefaultLeadTimeDays { get; set; } = 14;
    public decimal MonthlyCapacity { get; set; }
    public decimal AvailableCapacity { get; set; }
}

public class Component
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SupplyNodeId { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
}

public class Factory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SupplyNodeId { get; set; }
    public string Name { get; set; } = default!;
    public string Region { get; set; } = default!;
    public decimal DailyProductionCapacityUnits { get; set; }
}

public class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SupplyNodeId { get; set; }
    public string Name { get; set; } = default!;
    public string Region { get; set; } = default!;
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SupplyNodeId { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public decimal UnitRevenue { get; set; }
    public decimal DailyDemandUnits { get; set; }
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SupplyNodeId { get; set; }
    public string Name { get; set; } = default!;
    public string Segment { get; set; } = default!; // e.g. HIGH_VALUE, STANDARD
}

// Bill of materials: which components a product consumes, and in what quantity.
public class BillOfMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ComponentId { get; set; }
    public decimal QuantityPerUnit { get; set; } = 1;
}

public class InventoryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ComponentId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal DailyConsumption { get; set; }
    public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;
}
