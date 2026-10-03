using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Analytics.Application;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Modules.DigitalTwin.Domain;
using NEXUS.Modules.Forecasting.Application;
using NEXUS.Modules.Forecasting.Domain;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Modules.Simulation.Domain;

namespace NEXUS.Modules.DigitalTwin.Presentation;

public sealed record CommandCenterViewModel(
    EnterpriseStateDto State,
    EnterpriseGraphDto Graph,
    IReadOnlyList<Risk> ActiveRisks,
    IReadOnlyList< NEXUS.Modules.DecisionEngine.Domain.Decision> RecentDecisions,
    IReadOnlyList<SimulationRun> RecentSimulations,
    IReadOnlyList<Prediction> Predictions,
    IReadOnlyList<Forecast> DemandForecasts);

public sealed class CommandCenterController : Controller
{
    private readonly IDigitalTwinService _digitalTwinService;
    private readonly IDependencyGraphService _graphService;
    private readonly IDecisionEngineService _decisionService;
    private readonly ISimulationService _simulationService;
    private readonly IForecastingService _forecastingService;

    public CommandCenterController(
        IDigitalTwinService digitalTwinService,
        IDependencyGraphService graphService,
        IDecisionEngineService decisionService,
        ISimulationService simulationService,
        IForecastingService forecastingService)
    {
        _digitalTwinService = digitalTwinService;
        _graphService = graphService;
        _decisionService = decisionService;
        _simulationService = simulationService;
        _forecastingService = forecastingService;
    }

    [HttpGet("/")]
    [HttpGet("/CommandCenter")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var state = await _digitalTwinService.GetCurrentEnterpriseStateAsync(false, cancellationToken);
        var graph = await _graphService.GetEnterpriseGraphAsync(90, null, cancellationToken);
        var risks = await _digitalTwinService.GetActiveRisksAsync(cancellationToken);
        var decisions = await _decisionService.GetDecisionsAsync(5, cancellationToken);
        var sims = await _simulationService.GetSimulationRunsAsync(5, cancellationToken);
        var preds = await _forecastingService.GetLatestPredictionsAsync(cancellationToken);
        var forecasts = await _forecastingService.GetDemandForecastHorizonAsync(14, cancellationToken);

        var vm = new CommandCenterViewModel(
            state,
            graph,
            risks,
            decisions,
            sims,
            preds,
            forecasts);

        return View("~/Views/CommandCenter/Index.cshtml", vm);
    }
}
