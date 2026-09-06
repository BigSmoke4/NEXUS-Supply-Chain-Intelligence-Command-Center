using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Security;
using Xunit;

namespace Nexus.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task SeedAsync_CreatesExpectedReferenceScale_AndIsIdempotent()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        // Seed is a system bootstrap operation, so it intentionally runs
        // without a request tenant. After seeding, reopen the same store with
        // the real organization as the current tenant and verify the filtered
        // view that application requests receive.
        await using (var seedDb = new NexusDbContext(options, new UnresolvedTenant()))
            await SeedData.SeedAsync(seedDb);

        Guid seededOrgId;
        await using (var lookupDb = new NexusDbContext(options, new UnresolvedTenant()))
            seededOrgId = await lookupDb.Organizations.Select(o => o.Id).SingleAsync();

        await using var db = new NexusDbContext(options, new FixedCurrentTenant(seededOrgId));

        var firstCounts = new
        {
            Suppliers = await db.Suppliers.CountAsync(),
            Components = await db.Components.CountAsync(),
            Factories = await db.Factories.CountAsync(),
            Warehouses = await db.Warehouses.CountAsync(),
            Products = await db.Products.CountAsync(),
            Customers = await db.Customers.CountAsync(),
            Boms = await db.BillOfMaterials.CountAsync(),
            Documents = await db.KnowledgeDocuments.CountAsync()
        };

        Assert.Equal(25, firstCounts.Suppliers);
        Assert.Equal(40, firstCounts.Components);
        Assert.Equal(12, firstCounts.Factories);
        Assert.Equal(8, firstCounts.Warehouses);
        Assert.Equal(100, firstCounts.Products);
        Assert.Equal(20, firstCounts.Customers);
        Assert.True(firstCounts.Boms >= 100);
        Assert.Equal(2, firstCounts.Documents);

        await SeedData.SeedAsync(db);

        Assert.Equal(firstCounts.Suppliers, await db.Suppliers.CountAsync());
        Assert.Equal(firstCounts.Components, await db.Components.CountAsync());
        Assert.Equal(firstCounts.Products, await db.Products.CountAsync());
        Assert.Equal(firstCounts.Boms, await db.BillOfMaterials.CountAsync());
    }

    private sealed class UnresolvedTenant : ICurrentTenant
    {
        public Guid? OrganizationId => null;
        public bool IsResolved => false;
    }

}
