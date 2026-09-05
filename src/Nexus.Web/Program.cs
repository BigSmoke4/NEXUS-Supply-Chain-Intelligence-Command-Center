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
// MockLlmProvider is used because this build has no outbound network access.
// Swap for AnthropicLlmProvider (or another adapter) once a real API key and
// network egress are available - no other code needs to change.
builder.Services.AddSingleton<ILLMProvider, MockLlmProvider>();

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
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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

// ---- Dev-time database bootstrap + seed data (§54) ----
// SeedData only performs writes (Add/AddRange) plus one read against
// Organizations, which carries no OrganizationId column and therefore no
// tenant query filter - so it works correctly even with no ambient tenant
// resolved. If you extend SeedData with a filtered read, wrap it in
// HttpContextCurrentTenant.UseTenant(orgId) the same way the background
// worker does (see Modules/BackgroundJobs/SimulationJobQueue.cs).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

app.Run();

public partial class Program { } // exposed for WebApplicationFactory-based integration tests
