namespace Nexus.Web.Domain.Entities;

public enum SimulationJobStatus { Queued, Running, Completed, Failed }

/// <summary>
/// Durable record of a queued simulation run. The in-memory Channel in
/// SimulationJobQueue gives fast dispatch for the common case; this table is
/// what makes the queue survive a process restart - on startup,
/// SimulationBackgroundWorker re-enqueues anything left in Queued or Running
/// state (Running means the previous process died mid-job) so no job is
/// silently lost.
/// </summary>
public class SimulationJobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ScenarioId { get; set; }
    public SimulationJobStatus Status { get; set; } = SimulationJobStatus.Queued;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
