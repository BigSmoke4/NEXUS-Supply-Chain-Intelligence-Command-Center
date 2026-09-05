namespace Nexus.Web.Domain.Entities;

// A generic node in the supply-chain dependency graph. Concrete business
// entities (Supplier, Factory, ...) each own a SupplyNode via NodeRefId so the
// graph algorithms can stay generic while domain modules keep their own
// strongly-typed tables (see §21 database design).
public class SupplyNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public NodeType Type { get; set; }
    public Guid NodeRefId { get; set; } // FK into the owning table (Suppliers.Id, Factories.Id, etc.)
    public string Name { get; set; } = default!;
    public string? Region { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<SupplyEdge> OutgoingEdges { get; set; } = new();
    public List<SupplyEdge> IncomingEdges { get; set; } = new();
}

public class SupplyEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SourceNodeId { get; set; }
    public SupplyNode Source { get; set; } = default!;
    public Guid TargetNodeId { get; set; }
    public SupplyNode Target { get; set; } = default!;
    public EdgeType Type { get; set; }

    // Metadata per §5 - kept on the edge because relationship strength/terms
    // vary per supplier-component pair, not per node.
    public int? LeadTimeDays { get; set; }
    public decimal? CapacityPerMonth { get; set; }
    public decimal? UnitCost { get; set; }
    public double? ReliabilityPercent { get; set; }
    public int? MinimumOrderQuantity { get; set; }
    public string? ContractPriority { get; set; }
    public double Weight { get; set; } = 1.0; // used for critical-path / bottleneck scoring
}
