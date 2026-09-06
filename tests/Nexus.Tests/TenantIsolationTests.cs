using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Security;
using Xunit;

namespace Nexus.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task TenantFilter_HidesOtherOrganizationsAcrossCoreAndSimulationArtifacts()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var orgId = Guid.NewGuid();
        var otherOrg = Guid.NewGuid();

        await using (var setup = new NexusDbContext(options, new UnresolvedTenant()))
        {
            setup.Suppliers.Add(new Supplier { OrganizationId = orgId, Name = "Visible", Country = "Test" });
            setup.Suppliers.Add(new Supplier { OrganizationId = otherOrg, Name = "Hidden", Country = "Test" });
            setup.BillOfMaterials.Add(new BillOfMaterial { OrganizationId = otherOrg, ProductId = Guid.NewGuid(), ComponentId = Guid.NewGuid() });
            setup.ScenarioResults.Add(new ScenarioResult { OrganizationId = otherOrg, ScenarioId = Guid.NewGuid(), RevenueAtRisk = 999999m });
            await setup.SaveChangesAsync();
        }

        await using var db = new NexusDbContext(options, new FixedCurrentTenant(orgId));
        Assert.Single(await db.Suppliers.ToListAsync());
        Assert.Empty(await db.BillOfMaterials.ToListAsync());
        Assert.Empty(await db.ScenarioResults.ToListAsync());
    }

    private sealed class UnresolvedTenant : ICurrentTenant
    {
        public Guid? OrganizationId => null;
        public bool IsResolved => false;
    }

    [Fact]
    public async Task TenantBoundary_RejectsCrossTenantWrites()
    {
        var (db, orgId) = TestDb.Create();
        db.Suppliers.Add(new Supplier { OrganizationId = Guid.NewGuid(), Name = "Cross-tenant", Country = "Test" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TenantBoundary_AutoAssignsOrganizationForNewTenantOwnedArtifacts()
    {
        var (db, orgId) = TestDb.Create();
        var result = new ScenarioResult { ScenarioId = Guid.NewGuid(), RevenueAtRisk = 10m };
        db.ScenarioResults.Add(result);

        await db.SaveChangesAsync();

        Assert.Equal(orgId, result.OrganizationId);
        Assert.Same(result, await db.ScenarioResults.SingleAsync());
    }
}
