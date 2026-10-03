using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NEXUS.Modules.Analytics.Application;
using NEXUS.Modules.Analytics.Domain;
using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Modules.Optimization.Application;
using NEXUS.Modules.Optimization.Domain;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Modules.Simulation.Domain;
using Xunit;

namespace NEXUS.IntegrationTests;

public sealed class NexusWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            var pgEnv = Environment.GetEnvironmentVariable("NEXUS_TEST_POSTGRES_CONNECTION");
            var redisEnv = Environment.GetEnvironmentVariable("NEXUS_TEST_REDIS_CONNECTION");

            var settings = new Dictionary<string, string?>
            {
                ["Database:InitializeOnStartup"] = "true",
                ["Database:UseInMemory"] = string.IsNullOrWhiteSpace(pgEnv) ? "true" : "false"
            };

            if (!string.IsNullOrWhiteSpace(pgEnv))
            {
                settings["ConnectionStrings:DefaultConnection"] = pgEnv;
            }

            if (!string.IsNullOrWhiteSpace(redisEnv))
            {
                settings["ConnectionStrings:Redis"] = redisEnv;
            }

            config.AddInMemoryCollection(settings);
        });
    }
}

public sealed class NexusEnterpriseIntegrationTests : IClassFixture<NexusWebFactory>
{
    private readonly HttpClient _client;

    public NexusEnterpriseIntegrationTests(NexusWebFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HealthAndEnterpriseStateApi_ReturnSeededNexusGlobalIndustriesTelemetry()
    {
        var live = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var ready = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var state = await _client.GetFromJsonAsync<EnterpriseStateDto>("/api/enterprise/state");
        Assert.NotNull(state);
        Assert.Equal(50, state!.TotalSuppliers);
        Assert.Equal(20, state.TotalFactories);
        Assert.Equal(40, state.TotalWarehouses);
        Assert.Equal(100, state.TotalRoutes);
        Assert.Equal(500, state.TotalProducts);
        Assert.True(state.TotalDependencies >= 1000);
        Assert.True(state.ResilienceScore > 0m);
    }

    [Fact]
    public async Task AssetsAndDependencyGraphApis_InspectFactoryBAndRunImpactPropagation()
    {
        var assets = await _client.GetFromJsonAsync<List<AssetInspectionDto>>("/api/assets?limit=100");
        Assert.NotNull(assets);
        Assert.True(assets!.Count >= 50);

        var factoryB = await _client.GetFromJsonAsync<AssetInspectionDto>("/api/assets/FAC-002");
        Assert.NotNull(factoryB);
        Assert.Equal("FAC-002", factoryB!.AssetCode);
        Assert.Equal(78.0m, factoryB.UtilizationPct);
        Assert.Equal(12_000m, factoryB.Capacity);
        Assert.Equal(9_360m, factoryB.CurrentLoad);
        Assert.Equal(8, factoryB.DependentWarehousesCount);
        Assert.Equal(1_800_000m, factoryB.DailyRevenueExposureUsd);

        var graph = await _client.GetFromJsonAsync<EnterpriseGraphDto>("/api/dependency-graph?maxNodes=120");
        Assert.NotNull(graph);
        Assert.NotEmpty(graph!.Nodes);
        Assert.NotEmpty(graph.Edges);
        Assert.NotEmpty(graph.CriticalPathAssetCodes);

        var impact = await _client.GetFromJsonAsync<DependencyImpactAnalysisDto>("/api/dependency-graph/SUP-001/impact?reductionPct=40&durationDays=14");
        Assert.NotNull(impact);
        Assert.Equal("SUP-001", impact!.RootAssetCode);
        Assert.True(impact.TotalAffectedDownstreamNodes > 0);
    }

    [Fact]
    public async Task Scenario_Simulation_Optimization_And_DecisionApproval_EndToEndWorkflow_Succeeds()
    {
        // 1. Create Scenario
        var createScnResp = await _client.PostAsJsonAsync("/api/scenarios", new CreateScenarioCommand(
            "Integration Test: European Supplier -40% Capacity",
            ScenarioTypes.SupplierFailure,
            "SUP-001",
            14,
            60m,
            15m,
            10m));
        Assert.Equal(HttpStatusCode.Created, createScnResp.StatusCode);
        var scenario = await createScnResp.Content.ReadFromJsonAsync<Scenario>();
        Assert.NotNull(scenario);

        // 2. Run Monte Carlo Simulation
        var simResp = await _client.PostAsJsonAsync("/api/simulations", new RunSimulationCommand(
            scenario!.Id,
            MonteCarloIterations: 2000));
        Assert.Equal(HttpStatusCode.Created, simResp.StatusCode);
        var simRun = await simResp.Content.ReadFromJsonAsync<SimulationRun>();
        Assert.NotNull(simRun);
        Assert.Equal(2000, simRun!.MonteCarloIterations);
        Assert.True(simRun.MonteCarloMeanRevenueLossUsd > 0m);

        // 3. Run Google OR-Tools Multi-Objective Optimization
        var optResp = await _client.PostAsJsonAsync("/api/optimization", new RunOptimizationCommand(
            scenario.Id,
            simRun.Id,
            BudgetCeilingUsd: 2_200_000m,
            MinimumSlaTargetPct: 95.0m));
        Assert.Equal(HttpStatusCode.Created, optResp.StatusCode);
        var optRun = await optResp.Content.ReadFromJsonAsync<OptimizationRun>();
        Assert.NotNull(optRun);
        Assert.Equal("OPTIMAL", optRun!.SolverStatus);
        Assert.Equal(3, optRun.Results.Count);

        // 4. Generate Autonomous Decision & Approve via Human-in-the-Loop
        var decResp = await _client.PostAsJsonAsync("/api/decisions", new { scenarioId = scenario.Id });
        Assert.Equal(HttpStatusCode.Created, decResp.StatusCode);
        var decision = await decResp.Content.ReadFromJsonAsync<Decision>();
        Assert.NotNull(decision);
        Assert.Equal("PENDING_APPROVAL", decision!.Status);

        var approveResp = await _client.PostAsJsonAsync($"/api/decisions/{decision.Id}/approve", new
        {
            reviewerName = "Marcus Vance",
            reviewerRole = "DecisionApprover",
            comments = "Integration test approval"
        });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approved = await approveResp.Content.ReadFromJsonAsync<Decision>();
        Assert.Equal("APPROVED", approved!.Status);

        var execResp = await _client.PostAsync($"/api/decisions/{decision.Id}/execute", null);
        Assert.Equal(HttpStatusCode.OK, execResp.StatusCode);
    }

    [Fact]
    public async Task HeroDemonstration_AiAssistant_Forecasts_Resilience_And_BlackSwanApis_WorkEndToEnd()
    {
        var heroResp = await _client.PostAsync("/api/hero-demo/execute", null);
        Assert.Equal(HttpStatusCode.OK, heroResp.StatusCode);
        var heroReport = await heroResp.Content.ReadFromJsonAsync<HeroDemoExecutionReportDto>();
        Assert.NotNull(heroReport);
        Assert.Equal(15, heroReport!.Steps.Count);
        Assert.Equal("Strategy B", heroReport.RecommendedStrategy);

        var aiResp = await _client.PostAsJsonAsync("/api/decisions/assistant", new
        {
            question = "Our European supplier will lose 40% capacity for two weeks. What should we do?"
        });
        Assert.Equal(HttpStatusCode.OK, aiResp.StatusCode);
        var aiResult = await aiResp.Content.ReadFromJsonAsync<AiAssistantResponseDto>();
        Assert.NotNull(aiResult);
        Assert.Equal("SUP-001", aiResult!.IdentifiedTargetAssetCode);
        Assert.Equal(40m, aiResult.ParsedCapacityLossPct);
        Assert.Equal(14, aiResult.ParsedDurationDays);

        var resilience = await _client.GetFromJsonAsync<ResilienceScore>("/api/resilience");
        Assert.NotNull(resilience);
        Assert.True(resilience!.OverallScore > 0m);

        var blackSwanResp = await _client.PostAsJsonAsync("/api/analytics/black-swan", new BlackSwanStressRequestDto(
            -40m, 35m, -20m, 25m, -15m, 1000));
        Assert.Equal(HttpStatusCode.OK, blackSwanResp.StatusCode);
        var bs = await blackSwanResp.Content.ReadFromJsonAsync<BlackSwanStressResultDto>();
        Assert.NotNull(bs);
        Assert.Equal(1000, bs!.ScenariosEvaluated);
        Assert.True(bs.WorstCaseRevenueLossUsd > 0m);
    }

    [Fact]
    public async Task AllRazorMvcControlRoomPages_RenderSuccessfully()
    {
        var pages = new[]
        {
            "/",
            "/CommandCenter",
            "/DigitalTwin",
            "/DependencyGraph",
            "/ScenarioLab",
            "/Simulation",
            "/Optimization",
            "/DecisionEngine",
            "/Forecasting",
            "/Analytics",
            "/Execution",
            "/Feedback",
            "/Audit",
            "/Identity/Login"
        };

        foreach (var path in pages)
        {
            var resp = await _client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var html = await resp.Content.ReadAsStringAsync();
            Assert.Contains("NEXUS", html);
        }
    }
}
