using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Modules.Simulation.Domain;

namespace NEXUS.Modules.Simulation.Presentation;

public sealed record SimulationViewModel(
    SimulationRun ActiveRun,
    IReadOnlyList<SimulationRun> RecentRuns,
    IReadOnlyList<Scenario> Scenarios);

public sealed class SimulationController : Controller
{
    private readonly ISimulationService _simulationService;
    private readonly IScenarioService _scenarioService;

    public SimulationController(
        ISimulationService simulationService,
        IScenarioService scenarioService)
    {
        _simulationService = simulationService;
        _scenarioService = scenarioService;
    }

    [HttpGet("/Simulation")]
    public async Task<IActionResult> Index([FromQuery] Guid? id = null, CancellationToken cancellationToken = default)
    {
        var runs = await _simulationService.GetSimulationRunsAsync(20, cancellationToken);
        var scenarios = await _scenarioService.GetScenariosAsync(cancellationToken);
        var active = id.HasValue
            ? await _simulationService.GetSimulationRunByIdAsync(id.Value, cancellationToken) ?? runs.First()
            : runs.First();

        return View("~/Views/Simulation/Index.cshtml", new SimulationViewModel(active, runs, scenarios));
    }

    [HttpPost("/Simulation/Run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(Guid scenarioId, int monteCarloIterations = 10_000, CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var run = await _simulationService.ExecuteSimulationAsync(
            new RunSimulationCommand(scenarioId, monteCarloIterations, InitiatedBy: actor),
            null,
            cancellationToken);

        return RedirectToAction(nameof(Index), new { id = run.Id });
    }
}
