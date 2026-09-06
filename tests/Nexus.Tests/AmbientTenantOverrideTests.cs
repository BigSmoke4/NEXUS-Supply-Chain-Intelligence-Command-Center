using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Security;
using Xunit;

namespace Nexus.Tests;

/// <summary>
/// Reproduces and proves the fix for a real bug found while hardening the
/// background job queue: NexusDbContext resolved via DI inside a hosted
/// service (no HttpContext) previously always saw ICurrentTenant.OrganizationId
/// as null, so every tenant-filtered read silently returned zero rows -
/// meaning simulations run through the background path never attached their
/// result to the scenario. HttpContextCurrentTenant.UseTenant(...) fixes
/// this with an AsyncLocal ambient override; these tests exercise exactly
/// the failure mode and confirm the fix.
/// </summary>
public class AmbientTenantOverrideTests
{
    /// An accessor whose HttpContext is always null, simulating exactly the
    /// condition inside a BackgroundService scope.
    private class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = null;
    }

    [Fact]
    public async Task WithoutAmbientOverride_NoHttpContext_ReadsReturnZeroRows_FailsClosed()
    {
        var orgId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenant = new HttpContextCurrentTenant(new NullHttpContextAccessor());
        var db = new NexusDbContext(options, tenant);

        db.Suppliers.Add(new Supplier { OrganizationId = orgId, Name = "Test Supplier", Country = "Testland" });
        await db.SaveChangesAsync();

        // Same DbContext, same data, no ambient override, no HttpContext:
        // this is exactly the pre-fix background-worker condition.
        var found = await db.Suppliers.ToListAsync();

        Assert.Empty(found); // fails closed, not open - this is correct behavior absent the fix below
    }

    [Fact]
    public async Task WithAmbientOverride_NoHttpContext_ReadsResolveCorrectly()
    {
        var orgId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenant = new HttpContextCurrentTenant(new NullHttpContextAccessor());
        var db = new NexusDbContext(options, tenant);

        db.Suppliers.Add(new Supplier { OrganizationId = orgId, Name = "Test Supplier", Country = "Testland" });
        await db.SaveChangesAsync();

        List<Supplier> found;
        using (HttpContextCurrentTenant.UseTenant(orgId))
        {
            // This is what SimulationBackgroundWorker now does before every
            // DI-resolved NexusDbContext read: wrap the job in UseTenant(...).
            found = await db.Suppliers.ToListAsync();
        }

        Assert.Single(found);
        Assert.Equal("Test Supplier", found[0].Name);
    }

    [Fact]
    public async Task AmbientOverride_IsClearedAfterDispose()
    {
        var orgId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenant = new HttpContextCurrentTenant(new NullHttpContextAccessor());
        var db = new NexusDbContext(options, tenant);
        db.Suppliers.Add(new Supplier { OrganizationId = orgId, Name = "Test Supplier", Country = "Testland" });
        await db.SaveChangesAsync();

        using (HttpContextCurrentTenant.UseTenant(orgId))
        {
            Assert.Single(await db.Suppliers.ToListAsync());
        }

        // After the using block, the override must be gone - a background
        // worker processing job A for org 1 must not leak into job B for
        // org 2 processed on the same thread pool thread afterward.
        Assert.Empty(await db.Suppliers.ToListAsync());
    }
}
