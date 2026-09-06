using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Hubs;
using Nexus.Web.Modules.AI;
using Nexus.Web.Modules.AI.Agents;
using Nexus.Web.Modules.Alerts;
using Nexus.Web.Modules.Audit;
using Nexus.Web.Modules.BackgroundJobs;
using Nexus.Web.Modules.Knowledge;
using Nexus.Web.Modules.NaturalLanguage;
using Nexus.Web.Modules.Observability;
using Nexus.Web.Modules.Optimization;
using Nexus.Web.Modules.Reporting;
using Nexus.Web.Modules.Risk;
using Nexus.Web.Modules.Simulation;
using Nexus.Web.Modules.SupplyNetwork;
using Nexus.Web.Modules.Validation;
using Nexus.Web.Modules.WhatIf;
using Nexus.Web.Security;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Structured logging (§68) ----
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Nexus")
    .WriteTo.Console());

// ---- Database (§21) ----
builder.Services.AddDbContext<NexusDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Nexus")
        ?? "Host=localhost;Database=nexus;Username=nexus;Password=nexus"));

// ---- Multi-tenancy (§22): resolves OrganizationId from the authenticated
// user's claims and is consumed by NexusDbContext's global query filters. ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpContextCurrentTenant>();

// ---- Identity + RBAC (§23) ----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.Password.RequiredLength = 10;
        opt.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<NexusDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<NexusClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddAuthorization(options => options.AddNexusAuthorizationPolicies());

// ---- MVC + Razor ----
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ---- Domain / application services (real logic, §71) ----
builder.Services.AddScoped<IGraphService, GraphService>();
builder.Services.AddScoped<ISimulationEngine, SimulationEngine>();
builder.Services.AddScoped<IMitigationEngine, MitigationEngine>();
builder.Services.AddSingleton(new SupplierRiskWeights()); // configurable, see §7
builder.Services.AddScoped<IRiskScoringService, RiskScoringService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INaturalLanguageScenarioParser, NaturalLanguageScenarioParser>();
builder.Services.AddScoped<IDocumentRetrievalService, TfIdfDocumentRetrievalService>();
builder.Services.AddScoped<IWhatIfEngine, WhatIfEngine>();
builder.Services.AddSingleton<IExecutiveSummaryReportGenerator, QuestPdfExecutiveSummaryReportGenerator>();

// ---- Background job processing (§46): long-running simulations run off the
// request thread via a Channel-backed queue drained by a hosted worker. ----
builder.Services.AddSingleton<SimulationJobQueue>();
builder.Services.AddSingleton<ISimulationJobQueue>(sp => sp.GetRequiredService<SimulationJobQueue>());
builder.Services.AddHostedService<SimulationBackgroundWorker>();

builder.Services.AddScoped<IAlertService, AlertService>();

// ---- Input validation (§43) ----
builder.Services.AddValidatorsFromAssemblyContaining<RunScenarioRequestValidator>();

// ---- AI abstraction (§39-41) ----
// Provider selection is configuration-driven. Mock remains the safe default;
// selecting Anthropic wires the real Messages API adapter without requiring
// controllers, agents, or domain services to know the vendor.
var aiProvider = builder.Configuration["AI:Provider"]?.Trim().ToLowerInvariant() ?? "mock";
if (aiProvider == "anthropic")
{
    builder.Services.AddHttpClient<AnthropicLlmProvider>(client =>
    {
        client.BaseAddress = new Uri("https://api.anthropic.com");
        client.Timeout = TimeSpan.FromSeconds(90);
    });
    builder.Services.AddSingleton<ILLMProvider>(sp => sp.GetRequiredService<AnthropicLlmProvider>());
}
else
{
    builder.Services.AddSingleton<ILLMProvider, MockLlmProvider>();
}

// Specialist agents (§15/§16): registered as a collection so
// AgentOrchestrator can fan them out with Task.WhenAll without knowing the
// concrete set - add a new agent by adding one more AddScoped<ISpecialistAgent, X> line.
builder.Services.AddScoped<ISpecialistAgent, InventoryAgent>();
builder.Services.AddScoped<ISpecialistAgent, SupplierRiskAgent>();
builder.Services.AddScoped<ISpecialistAgent, TransportationAgent>();
builder.Services.AddScoped<ISpecialistAgent, FinancialImpactAgent>();
builder.Services.AddScoped<ISpecialistAgent, GraphIntelligenceAgent>();
builder.Services.AddScoped<ISpecialistAgent, ContractRetrievalAgent>();
builder.Services.AddScoped<ISpecialistAgent, ResearchAgent>();
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

// ---- Observability (§45) ----
builder.Services.AddSingleton<NexusMetrics>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Nexus.Web"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        // Swap AddConsoleExporter() for AddOtlpExporter() pointed at your
        // collector in production; console export keeps this runnable
        // out-of-the-box in dev/demo environments without extra infra.
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(NexusMetrics.MeterName)
        // AddAspNetCoreInstrumentation() on MeterProviderBuilder doesn't
        // exist in the resolved package version (confirmed by an actual CI
        // build failure - CS1929, that overload only exists on
        // TracerProviderBuilder). ASP.NET Core's own request/hosting
        // metrics are exposed via named Meters built into the framework
        // since .NET 8 - add them directly instead of via the
        // instrumentation package's (trace-only, here) helper.
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
        // Runtime metrics are emitted through System.Runtime meters; the
        // resolved OpenTelemetry.Runtime 1.9 package does not expose
        // AddRuntimeInstrumentation() on MeterProviderBuilder.
        .AddMeter("System.Runtime"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (app.Configuration.GetValue("HttpsRedirection:Enabled", app.Environment.IsDevelopment()))
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=CommandCenter}/{action=Index}/{id?}");
app.MapHub<SimulationHub>("/hubs/simulation");

// ---- Health checks (§66) ----
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (NexusDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
});

// ---- Database bootstrap + deterministic demo seed (§54) ----
// The reference repository intentionally keeps migrations optional so a fresh
// checkout can boot without a generated migration artifact. When startup
// initialization is enabled, apply migrations if present; otherwise EnsureCreated
// creates the model directly. This is intended for the demo/reference deployment
// path. Production deployments should generate, review, and apply migrations in
// their release pipeline rather than relying on automatic startup DDL.
if (app.Configuration.GetValue("Database:InitializeOnStartup", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
    var migrations = db.Database.GetMigrations();
    if (migrations.Any())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    await SeedData.SeedAsync(db);
}

app.Run();

public partial class Program { } // exposed for WebApplicationFactory-based integration tests
