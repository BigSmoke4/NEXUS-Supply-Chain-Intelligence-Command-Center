namespace Nexus.Web.Domain.Entities;

/// Immutable audit record (§38). Every field the brief calls for: who, what,
/// when, before/after, and for AI-originated actions, which agent/evidence
/// backed the decision that a human then approved.
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;

    public string? UserId { get; set; }
    public string Action { get; set; } = default!;       // e.g. "MitigationStrategy.Approve"
    public string EntityType { get; set; } = default!;    // e.g. "MitigationStrategy"
    public Guid EntityId { get; set; }

    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }

    // Populated when the action follows an AI recommendation, per §38's
    // requirement to record agent/tools/evidence/confidence alongside the
    // human decision that acted on them.
    public string? AiAgent { get; set; }
    public double? AiConfidence { get; set; }
    public int? AiEvidenceCount { get; set; }
}
