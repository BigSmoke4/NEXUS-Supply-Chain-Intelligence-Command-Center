using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Web.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nexus.IntegrationTests;

/// <summary>
/// Real Postgres via Testcontainers - not SQLite, not InMemory. Catches
/// anything that's Postgres-specific (column types, migrations, concurrency
/// tokens) that the unit test suite's InMemory provider cannot, by design,
/// ever catch. This is the piece that closes the "not run against real
/// Postgres" gap named in the README.
/// </summary>
public sealed class NexusWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("nexus_integration_test")
        .WithUsername("nexus")
        .WithPassword("nexus")
        .Build();

    public NexusWebApplicationFactory()
    {
        // Start the database before WebApplicationFactory can construct the
        // host. Keeping this lifecycle inside the factory avoids xUnit's
        // IAsyncLifetime fixture-disposal ordering interacting with
        // WebApplicationFactory/TestServer.
        _postgres.StartAsync().GetAwaiter().GetResult();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // triggers migration + seed on startup, same as local dev

        builder.UseSetting("ConnectionStrings:Nexus", _postgres.GetConnectionString());

        builder.ConfigureServices(services =>
        {
            // Ensure the DbContext is definitely pointed at the container,
            // even if something upstream already registered a different
            // connection string before this runs.
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<NexusDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<NexusDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
        });
    }
}
