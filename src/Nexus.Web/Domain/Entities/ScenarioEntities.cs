namespace Nexus.Web.Domain.Entities;

public class Disruption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public DisruptionType Type { get; set; }
    public Guid AffectedNodeId { get; set; } // SupplyNode.Id
    public double CapacityReductionPercent { get; set; } // 0-100
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public double Probability { get; set; } = 1.0;
    public double Confidence { get; set; } = 0.9;
    public string? Assumptions { get; set; }
}

public class Scenario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Disruption> Disruptions { get; set; } = new();
    public ScenarioResult? LastResult { get; set; }
}

// Persisted output of the deterministic simulation engine (§10-11).
// This is the "calculated facts" layer the AI is only allowed to reason over,
// never invent (§71).
public class ScenarioResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioId { get; set; }
    public DateTime RunAtUtc { get; set; } = DateTime.UtcNow;

    public decimal RevenueAtRisk { get; set; }
    public int ProductsAffected { get; set; }
    public int CustomersAffected { get; set; }
    public int RecoveryDays { get; set; }
    public double ServiceLevelPercent { get; set; }
    public double OverallRiskScore { get; set; }

    public List<StockoutEvent> StockoutEvents { get; set; } = new();
    public List<TimelineEvent> Timeline { get; set; } = new();
    public List<MitigationStrategy> Strategies { get; set; } = new();
    public List<CascadeStep> CascadeSteps { get; set; } = new();
}

public class StockoutEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioResultId { get; set; }
    public Guid ComponentId { get; set; }
    public Guid WarehouseId { get; set; }
    public int StockoutDayOffset { get; set; } // days from scenario start
}

public class TimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioResultId { get; set; }
    public int DayOffset { get; set; }
    public string Label { get; set; } = default!;
    public string Severity { get; set; } = "info"; // info | warning | critical
}

/// One hop of the actual disruption propagation path through the supply
/// graph (§56), computed by the deterministic simulation engine - not
/// choreographed in the UI. StageOrder groups nodes by how many hops
/// downstream of the disrupted node they are (0 = the disrupted node
/// itself), so the front end can animate stage-by-stage.
public class CascadeStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioResultId { get; set; }
    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = default!;
    public string NodeType { get; set; } = default!;
    public int StageOrder { get; set; }
}

public class MitigationStrategy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScenarioResultId { get; set; }
    public MitigationType Type { get; set; }
    public string Description { get; set; } = default!;
    public decimal EstimatedRevenueLossAfter { get; set; }
    public decimal AdditionalCost { get; set; }
    public int RecoveryDaysAfter { get; set; }
    public double ConfidencePercent { get; set; }
    public int EvidenceCount { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
}
