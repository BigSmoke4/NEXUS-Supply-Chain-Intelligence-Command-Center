using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;

namespace NEXUS.Modules.ScenarioManagement.Presentation;

public sealed record ScenarioLabViewModel(
    IReadOnlyList<Scenario> Scenarios,
    IReadOnlyList<AssetInspectionDto> AvailableAssets);

public sealed class ScenarioLabController : Controller
{
    private readonly IScenarioService _scenarioService;
    private readonly IAssetManagementService _assetService;

    public ScenarioLabController(
        IScenarioService scenarioService,
        IAssetManagementService assetService)
    {
        _scenarioService = scenarioService;
        _assetService = assetService;
    }

    [HttpGet("/ScenarioLab")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var scenarios = await _scenarioService.GetScenariosAsync(cancellationToken);
        var assets = await _assetService.GetAssetsAsync(limit: 80, cancellationToken: cancellationToken);
        return View("~/Views/ScenarioLab/Index.cshtml", new ScenarioLabViewModel(scenarios, assets));
    }

    [HttpPost("/ScenarioLab/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string name,
        string scenarioType,
        string targetAssetCode,
        int durationDays,
        decimal supplierCapacityMultiplierPct,
        decimal demandDeltaPct,
        decimal transportCostDeltaPct,
        decimal factoryCapacityDeltaPct = 0m,
        decimal energyCostDeltaPct = 0m,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        await _scenarioService.CreateScenarioAsync(
            new CreateScenarioCommand(
                name,
                scenarioType,
                targetAssetCode,
                durationDays,
                supplierCapacityMultiplierPct,
                demandDeltaPct,
                transportCostDeltaPct,
                factoryCapacityDeltaPct,
                energyCostDeltaPct,
                description,
                false,
                actor),
            cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
