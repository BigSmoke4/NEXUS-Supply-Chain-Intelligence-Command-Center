using Microsoft.EntityFrameworkCore;
using Nexus.Web.Domain.Entities;
using Xunit;

namespace Nexus.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task TenantFilter_HidesOtherOrganizationsAcrossCoreAndSimulationArtifacts()
    {
        var (db, orgId) = TestDb.Create();
        var otherOrg = Guid.NewGuid();

        db.Suppliers.Add(new Supplier { OrganizationId = orgId, Name = "Visible", Country = "Test" });
        db.Suppliers.Add(new Supplier { OrganizationId = otherOrg, Name = "Hidden", Country = "Test" });
        db.BillOfMaterials.Add(new BillOfMaterial { OrganizationId = otherOrg, ProductId = Guid.NewGuid(), ComponentId = Guid.NewGuid() });
        db.ScenarioResults.Add(new ScenarioResult { OrganizationId = otherOrg, ScenarioId = Guid.NewGuid(), RevenueAtRisk = 999999m });
        await db.SaveChangesAsync();

        Assert.Single(await db.Suppliers.ToListAsync());
        Assert.Empty(await db.BillOfMaterials.ToListAsync());
        Assert.Empty(await db.ScenarioResults.ToListAsync());
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
