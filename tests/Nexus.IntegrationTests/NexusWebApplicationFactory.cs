using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexus.Web.Data;
using Testcontainers.PostgreSql;

namespace Nexus.IntegrationTests;

/// <summary>
/// Integration host backed by an isolated real PostgreSQL Testcontainer.
/// Database initialization is deliberately performed by a hosted initializer
/// inside the TestServer host so startup failures surface as normal test
/// exceptions instead of leaving WebApplicationFactory with a disposed server.
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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Do not run Program's development-only bootstrap during host startup.
        // The test initializer below owns database setup and runs as part of
        // the same host lifecycle, after all services have been registered.
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

            services.AddHostedService<TestDatabaseInitializer>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        // Let WebApplicationFactory shut down the host/TestServer first. Only
        // then dispose PostgreSQL, so no request can observe a dependency being
        // torn down underneath an active TestServer.
        base.Dispose(disposing);
        if (disposing)
            _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class TestDatabaseInitializer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public TestDatabaseInitializer(IServiceScopeFactory scopeFactory)
            => _scopeFactory = scopeFactory;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();

            var migrations = db.Database.GetMigrations().ToList();
            if (migrations.Count > 0)
                await db.Database.MigrateAsync(cancellationToken);
            else
                await db.Database.EnsureCreatedAsync(cancellationToken);

            await SeedData.SeedAsync(db);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
