using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Analytics.Application;
using NEXUS.Modules.Analytics.Infrastructure;
using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Modules.AssetManagement.Infrastructure;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Infrastructure;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DecisionEngine.Infrastructure;
using NEXUS.Modules.DemandManagement.Application;
using NEXUS.Modules.DemandManagement.Infrastructure;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.DependencyGraph.Infrastructure;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Modules.DigitalTwin.Infrastructure;
using NEXUS.Modules.Execution.Application;
using NEXUS.Modules.Execution.Infrastructure;
using NEXUS.Modules.Explainability.Application;
using NEXUS.Modules.Explainability.Infrastructure;
using NEXUS.Modules.Feedback.Application;
using NEXUS.Modules.Feedback.Infrastructure;
using NEXUS.Modules.Forecasting.Application;
using NEXUS.Modules.Forecasting.Infrastructure;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Inventory.Application;
using NEXUS.Modules.Inventory.Infrastructure;
using NEXUS.Modules.Optimization.Application;
using NEXUS.Modules.Optimization.Infrastructure;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Infrastructure;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Infrastructure;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Modules.Simulation.Infrastructure;
using NEXUS.Modules.SupplyChain.Application;
using NEXUS.Modules.SupplyChain.Infrastructure;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;
using NEXUS.Shared.Kernel;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddDbContext<NexusDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var pgConn = Environment.GetEnvironmentVariable("NEXUS_TEST_POSTGRES_CONNECTION")
                 ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                 ?? config.GetConnectionString("DefaultConnection");
    var inMemory = config.GetValue<bool>("Database:UseInMemory");

    if (!inMemory && !string.IsNullOrWhiteSpace(pgConn))
    {
        options.UseNpgsql(pgConn);
    }
    else
    {
        options.UseInMemoryDatabase("NexusEnterpriseTwinDb");
    }
});

var redisConnection = Environment.GetEnvironmentVariable("NEXUS_TEST_REDIS_CONNECTION")
                      ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
                      ?? builder.Configuration.GetConnectionString("Redis")
                      ?? builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "NEXUS:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services
    .AddIdentity<NexusUser, NexusRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<NexusDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Login";
    options.AccessDeniedPath = "/Identity/Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthorization(options =>
{
    foreach (var role in NexusRoles.All)
    {
        options.AddPolicy($"Require{role}", policy => policy.RequireRole(role, NexusRoles.Administrator));
    }
});

builder.Services.AddHttpClient();

// Shared Infrastructure & Background Processing
builder.Services.AddSingleton<INexusCacheService, RedisNexusCacheService>();
builder.Services.AddSingleton<BackgroundJobService>();
builder.Services.AddSingleton<IBackgroundJobService>(sp => sp.GetRequiredService<BackgroundJobService>());
builder.Services.AddHostedService<NexusBackgroundHostedService>();
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

// 18 Modular Monolith Application & Infrastructure Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IAssetManagementService, AssetManagementService>();
builder.Services.AddScoped<ISupplyChainService, SupplyChainService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IDemandService, DemandService>();
builder.Services.AddScoped<IDependencyGraphService, DependencyGraphService>();
builder.Services.AddScoped<IDigitalTwinService, DigitalTwinService>();
builder.Services.AddScoped<IHeroDemonstrationService, HeroDemonstrationService>();
builder.Services.AddScoped<IScenarioService, ScenarioService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IOptimizationService, OptimizationService>();
builder.Services.AddScoped<IExplainabilityService, ExplainabilityService>();
builder.Services.AddScoped<IDecisionEngineService, DecisionEngineService>();
builder.Services.AddScoped<IForecastingService, ForecastingService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IExecutionService, ExecutionService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "LIVE", application = "NEXUS Autonomous Enterprise Decision Engine", timestampUtc = DateTime.UtcNow }));
app.MapGet("/health/ready", async (NexusDbContext db) =>
{
    var orgCount = await db.Organizations.CountAsync();
    return Results.Ok(new { status = "READY", organizations = orgCount, timestampUtc = DateTime.UtcNow });
});

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=CommandCenter}/{action=Index}/{id?}");

if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    await NexusSeedData.InitializeAsync(app.Services);
}

app.Run();

public partial class Program { }
