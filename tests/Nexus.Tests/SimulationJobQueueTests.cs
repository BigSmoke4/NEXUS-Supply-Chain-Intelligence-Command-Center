using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.BackgroundJobs;
using Xunit;

namespace Nexus.Tests;

public class SimulationJobQueueTests
{
    [Fact]
    public async Task EnqueueAsync_PersistsDurableJobRecord_BeforeSignalingChannel()
    {
        var (db, orgId) = TestDb.Create();
        var scenarioId = Guid.NewGuid();

        var queue = new SimulationJobQueue(new SingleContextScopeFactory(db), new Nexus.Web.Modules.Observability.NexusMetrics());
        await queue.EnqueueAsync(new SimulationJob(orgId, scenarioId));

        // This is the durability guarantee: even if nothing ever reads from
        // the in-memory channel again (e.g. the process crashes), the job's
        // existence is recorded and recoverable from the database.
        var record = Assert.Single(db.SimulationJobRecords);
        Assert.Equal(scenarioId, record.ScenarioId);
        Assert.Equal(SimulationJobStatus.Queued, record.Status);
    }

    [Fact]
    public async Task RecoveryQuery_FindsQueuedAndRunningJobs_AcrossTenants_ButNotCompleted()
    {
        var (db, orgId) = TestDb.Create();

        db.SimulationJobRecords.AddRange(
            new SimulationJobRecord { OrganizationId = orgId, ScenarioId = Guid.NewGuid(), Status = SimulationJobStatus.Queued },
            new SimulationJobRecord { OrganizationId = orgId, ScenarioId = Guid.NewGuid(), Status = SimulationJobStatus.Running },
            new SimulationJobRecord { OrganizationId = orgId, ScenarioId = Guid.NewGuid(), Status = SimulationJobStatus.Completed },
            new SimulationJobRecord { OrganizationId = Guid.NewGuid(), ScenarioId = Guid.NewGuid(), Status = SimulationJobStatus.Queued });
        await db.SaveChangesAsync();

        // Mirrors SimulationBackgroundWorker.RecoverUnfinishedJobsAsync's
        // query exactly: IgnoreQueryFilters (system-level recovery scan
        // across all tenants) + Queued/Running only.
        var unfinished = await db.SimulationJobRecords
            .IgnoreQueryFilters()
            .Where(r => r.Status == SimulationJobStatus.Queued || r.Status == SimulationJobStatus.Running)
            .ToListAsync();

        Assert.Equal(3, unfinished.Count); // 2 from orgId + 1 from the other tenant, 0 completed
        Assert.DoesNotContain(unfinished, r => r.Status == SimulationJobStatus.Completed);
    }

    /// Test double standing in for IServiceScopeFactory: always hands back a
    /// scope whose NexusDbContext is the single shared test context, since
    /// SimulationJobQueue only needs the DbContext, not a full DI container.
    private class SingleContextScopeFactory : IServiceScopeFactory
    {
        private readonly NexusDbContext _db;
        public SingleContextScopeFactory(NexusDbContext db) => _db = db;
        public IServiceScope CreateScope() => new FakeScope(_db);

        private class FakeScope : IServiceScope
        {
            public FakeScope(NexusDbContext db) => ServiceProvider = new FakeProvider(db);
            public IServiceProvider ServiceProvider { get; }
            public void Dispose() { }
        }

        private class FakeProvider : IServiceProvider
        {
            private readonly NexusDbContext _db;
            public FakeProvider(NexusDbContext db) => _db = db;
            public object? GetService(Type serviceType) => serviceType == typeof(NexusDbContext) ? _db : null;
        }
    }
}
