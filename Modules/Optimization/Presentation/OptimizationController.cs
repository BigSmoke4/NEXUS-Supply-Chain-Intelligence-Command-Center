using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Optimization.Application;
using NEXUS.Modules.Optimization.Domain;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;

namespace NEXUS.Modules.Optimization.Presentation;

public sealed record OptimizationViewModel(
    OptimizationRun ActiveRun,
    IReadOnlyList<OptimizationRun> RecentRuns,
    IReadOnlyList<Scenario> Scenarios,
    MitigationPortfolioResultDto MitigationPortfolio);

public sealed class OptimizationController : Controller
{
    private readonly IOptimizationService _optimizationService;
    private readonly IScenarioService _scenarioService;

    public OptimizationController(
        IOptimizationService optimizationService,
        IScenarioService scenarioService)
    {
        _optimizationService = optimizationService;
        _scenarioService = scenarioService;
    }

    [HttpGet("/Optimization")]
    public async Task<IActionResult> Index([FromQuery] Guid? id = null, [FromQuery] decimal budgetUsd = 2_800_000m, CancellationToken cancellationToken = default)
    {
        var runs = await _optimizationService.GetOptimizationRunsAsync(20, cancellationToken);
        var scenarios = await _scenarioService.GetScenariosAsync(cancellationToken);
        var active = id.HasValue
            ? await _optimizationService.GetOptimizationRunByIdAsync(id.Value, cancellationToken) ?? runs.First()
            : runs.First();
        var portfolio = await _optimizationService.OptimizeMitigationPortfolioAsync(budgetUsd, cancellationToken);

        return View("~/Views/Optimization/Index.cshtml", new OptimizationViewModel(active, runs, scenarios, portfolio));
    }

    [HttpPost("/Optimization/Run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(
        Guid scenarioId,
        decimal budgetCeilingUsd = 2_200_000m,
        decimal minimumSlaTargetPct = 95.0m,
        CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var run = await _optimizationService.ExecuteMultiObjectiveOptimizationAsync(
            new RunOptimizationCommand(scenarioId, null, budgetCeilingUsd, minimumSlaTargetPct, InitiatedBy: actor),
            null,
            cancellationToken);

        return RedirectToAction(nameof(Index), new { id = run.Id });
    }
}
