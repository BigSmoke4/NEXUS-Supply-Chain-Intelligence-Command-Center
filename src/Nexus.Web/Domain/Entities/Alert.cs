namespace Nexus.Web.Domain.Entities;

public enum AlertSeverity { Info, Warning, Critical }
public enum AlertState { Open, Acknowledged, Resolved }

/// Intelligent alert (§36): generated deterministically from simulation
/// results, not by the LLM. Supports the acknowledgement/resolution
/// lifecycle the brief calls for.
public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? ScenarioId { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertState State { get; set; } = AlertState.Open;
    public string Title { get; set; } = default!;
    public string Detail { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
