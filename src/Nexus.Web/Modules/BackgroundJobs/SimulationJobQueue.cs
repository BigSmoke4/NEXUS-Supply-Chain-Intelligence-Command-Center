using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Hubs;
using Nexus.Web.Modules.Alerts;
using Nexus.Web.Modules.Observability;
using Nexus.Web.Modules.Optimization;
using Nexus.Web.Modules.Simulation;
using Nexus.Web.Security;

namespace Nexus.Web.Modules.BackgroundJobs;

/// <summary>
/// A single simulation run to be executed off the request thread (§46).
/// Carries OrganizationId (not just ScenarioId) because the background
/// worker has no HttpContext to resolve the tenant from - see
/// HttpContextCurrentTenant.UseTenant, which this module relies on to make
/// its DI-resolved NexusDbContext instances tenant-correct.
/// </summary>
public record SimulationJob(Guid OrganizationId, Guid ScenarioId);

/// <summary>
/// Queues simulation jobs for the background worker. Every enqueue writes a
/// durable SimulationJobRecord row *before* pushing to the in-memory
/// Channel, so a job survives a process crash between enqueue and
/// completion - SimulationBackgroundWorker's startup recovery pass picks up
/// anything left in Queued/Running state. The Channel exists purely for
/// fast in-process dispatch; the database row is the source of truth.
/// </summary>
public interface ISimulationJobQueue
{
    Task EnqueueAsync(SimulationJob job, CancellationToken ct = default);
}

public class SimulationJobQueue : ISimulationJobQueue
{
    private readonly Channel<SimulationJob> _channel = Channel.CreateUnbounded<SimulationJob>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NexusMetrics _metrics;

    public SimulationJobQueue(IServiceScopeFactory scopeFactory, NexusMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
    }

    public async Task EnqueueAsync(SimulationJob job, CancellationToken ct = default)
    {
        using (HttpContextCurrentTenant.UseTenant(job.OrganizationId))
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.SimulationJobRecords.Add(new SimulationJobRecord
            {
                OrganizationId = job.OrganizationId, ScenarioId = job.ScenarioId, Status = SimulationJobStatus.Queued
            });
            await db.SaveChangesAsync(ct);
        }

        _metrics.SimulationQueueDepth.Add(1);
        await _channel.Writer.WriteAsync(job, ct);
    }

    /// Pushes directly to the in-memory channel without creating a new
    /// durable record - used only by the worker's startup recovery pass,
    /// which is re-enqueuing jobs whose record already exists.
    internal ValueTask RequeueExistingAsync(SimulationJob job, CancellationToken ct) =>
        _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<SimulationJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Hosted background service (§46) that drains the simulation queue. Two
/// things make this a real, not decorative, implementation of "background
/// processing that survives beyond a single request":
///
/// 1. Every job is wrapped in HttpContextCurrentTenant.UseTenant(job.OrganizationId)
///    before any DI-resolved NexusDbContext is created, so multi-tenant
///    query filters resolve correctly with no HttpContext present. Without
///    this, every read the worker does silently returns zero rows.
/// 2. On startup, a recovery pass re-enqueues any SimulationJobRecord left
///    in Queued or Running state (Running = the previous process died
///    mid-job), so a restart doesn't silently drop in-flight work.
/// </summary>
public class SimulationBackgroundWorker : BackgroundService
{
    private readonly SimulationJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<SimulationHub> _hub;
    private readonly NexusMetrics _metrics;
    private readonly ILogger<SimulationBackgroundWorker> _logger;

    public SimulationBackgroundWorker(
        SimulationJobQueue queue, IServiceScopeFactory scopeFactory,
        IHubContext<SimulationHub> hub, NexusMetrics metrics, ILogger<SimulationBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverUnfinishedJobsAsync(stoppingToken);

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            using var _ = HttpContextCurrentTenant.UseTenant(job.OrganizationId);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();

            var record = await db.SimulationJobRecords
                .Where(r => r.ScenarioId == job.ScenarioId && r.Status != SimulationJobStatus.Completed)
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync(stoppingToken);

            try
            {
                if (record is not null)
                {
                    record.Status = SimulationJobStatus.Running;
                    record.StartedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }

                await _hub.Clients.Group($"scenario-{job.ScenarioId}").SendAsync("SimulationStarted", job.ScenarioId, stoppingToken);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var simulation = scope.ServiceProvider.GetRequiredService<ISimulationEngine>();
                var mitigation = scope.ServiceProvider.GetRequiredService<IMitigationEngine>();
                var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();

                var result = await simulation.RunAsync(job.ScenarioId, stoppingToken);
                result.Strategies = mitigation.GenerateStrategies(result, alternativeSupplierCount: 1);
                stopwatch.Stop();
                _metrics.SimulationDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
                _metrics.ScenariosSimulated.Add(1);

                db.ScenarioResults.Add(result);
                var scenario = await db.Scenarios.FirstOrDefaultAsync(s => s.Id == job.ScenarioId, stoppingToken);
                if (scenario is null)
                {
                    _logger.LogWarning("Scenario {ScenarioId} not found when completing simulation job", job.ScenarioId);
                }
                else
                {
                    scenario.LastResult = result;
                    await db.SaveChangesAsync(stoppingToken);
                    await alerts.GenerateFromScenarioResultAsync(scenario.OrganizationId, scenario.Id, result, stoppingToken);
                }

                if (record is not null)
                {
                    record.Status = SimulationJobStatus.Completed;
                    record.CompletedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }

                await _hub.Clients.Group($"scenario-{job.ScenarioId}").SendAsync("SimulationCompleted", job.ScenarioId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Resilience (§52): a single failed job must not crash the
                // worker or block subsequent jobs in the queue.
                _logger.LogError(ex, "Simulation job for scenario {ScenarioId} failed", job.ScenarioId);
                _metrics.SimulationJobsFailed.Add(1);

                if (record is not null)
                {
                    record.Status = SimulationJobStatus.Failed;
                    record.ErrorMessage = ex.Message;
                    record.CompletedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }

                await _hub.Clients.Group($"scenario-{job.ScenarioId}").SendAsync("SimulationFailed", job.ScenarioId, stoppingToken);
            }
            finally
            {
                _metrics.SimulationQueueDepth.Add(-1);
            }
        }
    }

    private async Task RecoverUnfinishedJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();

        // IgnoreQueryFilters: this runs before any tenant is known - it is
        // deliberately a system-level, cross-tenant recovery scan, not a
        // per-request read, so bypassing the tenant filter here is correct
        // rather than a leak (each recovered job still carries its own
        // OrganizationId and is processed under that tenant's ambient scope).
        var unfinished = await db.SimulationJobRecords
            .IgnoreQueryFilters()
            .Where(r => r.Status == SimulationJobStatus.Queued || r.Status == SimulationJobStatus.Running)
            .ToListAsync(ct);

        if (unfinished.Count == 0) return;

        _logger.LogInformation("Recovering {Count} unfinished simulation job(s) after restart", unfinished.Count);
        foreach (var record in unfinished)
            await _queue.RequeueExistingAsync(new SimulationJob(record.OrganizationId, record.ScenarioId), ct);
    }
}
