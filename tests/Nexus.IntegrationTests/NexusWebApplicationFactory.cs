using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Nexus.Web.Data;
using Nexus.Web.Security;
using Testcontainers.PostgreSql;

namespace Nexus.IntegrationTests;

/// <summary>
/// Integration host backed by an isolated real PostgreSQL Testcontainer.
/// PostgreSQL is started and the schema/seed are prepared before
/// WebApplicationFactory starts the ASP.NET host. This is intentional: doing
/// database DDL from an IHostedService makes TestServer startup cancellation
/// race with Testcontainers/EF Core on GitHub-hosted runners.
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
        _postgres.StartAsync().GetAwaiter().GetResult();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var tenant = new HttpContextCurrentTenant(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        using var db = new NexusDbContext(options, tenant);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count > 0)
            db.Database.MigrateAsync(timeout.Token).GetAwaiter().GetResult();
        else
            db.Database.EnsureCreatedAsync(timeout.Token).GetAwaiter().GetResult();

        SeedData.SeedAsync(db).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program's normal Development bootstrap is disabled. The factory has
        // already initialized the isolated database before TestServer starts.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Nexus", _postgres.GetConnectionString());
        builder.UseSetting("Database:InitializeOnStartup", "false");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<NexusDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<NexusDbContext>(opt =>
                opt.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        // Shut down TestServer/host first, then PostgreSQL. This guarantees
        // the dependency outlives every request made by the test client.
        base.Dispose(disposing);
        if (disposing)
            _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
