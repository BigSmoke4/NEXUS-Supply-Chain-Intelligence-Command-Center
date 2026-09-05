using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Security;

namespace Nexus.Tests;

/// <summary>
/// Builds an isolated in-memory NexusDbContext per test, with a
/// FixedCurrentTenant so the global tenant query filters (see
/// NexusDbContext.OnModelCreating) behave exactly as they do against
/// PostgreSQL in production - tests exercise the real filtering behavior,
/// not a bypassed version of it.
/// </summary>
public static class TestDb
{
    public static (NexusDbContext db, Guid organizationId) Create()
    {
        var orgId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new NexusDbContext(options, new FixedCurrentTenant(orgId));
        return (db, orgId);
    }
}
