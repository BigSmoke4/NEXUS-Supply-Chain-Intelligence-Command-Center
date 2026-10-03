using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Modules.DependencyGraph.Application;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Modules.SupplyChain.Application;

namespace NEXUS.Modules.DigitalTwin.Presentation;

public sealed record DigitalTwinViewModel(
    EnterpriseStateDto State,
    EnterpriseGraphDto Graph,
    AssetInspectionDto SelectedAsset,
    DependencyImpactAnalysisDto ImpactAnalysis,
    SupplyChainOverviewDto SupplyChain);

public sealed class DigitalTwinController : Controller
{
    private readonly IDigitalTwinService _digitalTwinService;
    private readonly IDependencyGraphService _graphService;
    private readonly IAssetManagementService _assetService;
    private readonly ISupplyChainService _supplyChainService;

    public DigitalTwinController(
        IDigitalTwinService digitalTwinService,
        IDependencyGraphService graphService,
        IAssetManagementService assetService,
        ISupplyChainService supplyChainService)
    {
        _digitalTwinService = digitalTwinService;
        _graphService = graphService;
        _assetService = assetService;
        _supplyChainService = supplyChainService;
    }

    [HttpGet("/DigitalTwin")]
    public async Task<IActionResult> Index([FromQuery] string? select = "FAC-002", CancellationToken cancellationToken = default)
    {
        var state = await _digitalTwinService.GetCurrentEnterpriseStateAsync(false, cancellationToken);
        var graph = await _graphService.GetEnterpriseGraphAsync(140, null, cancellationToken);
        var selectedCode = string.IsNullOrWhiteSpace(select) ? "FAC-002" : select.Trim().ToUpperInvariant();
        var asset = await _assetService.GetAssetByIdOrCodeAsync(selectedCode, cancellationToken)
                    ?? await _assetService.GetAssetByIdOrCodeAsync("FAC-002", cancellationToken)
                    ?? (await _assetService.GetAssetsAsync(limit: 1, cancellationToken: cancellationToken)).First();
        var impact = await _graphService.AnalyzeImpactAsync(asset.AssetCode, 40m, 14, cancellationToken);
        var supplyChain = await _supplyChainService.GetOverviewAsync(cancellationToken);

        return View("~/Views/DigitalTwin/Index.cshtml", new DigitalTwinViewModel(state, graph, asset, impact, supplyChain));
    }

    [HttpPost("/DigitalTwin/UpdateCapacity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCapacity(string assetCode, decimal availabilityPct, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        await _assetService.UpdateAssetCapacityAvailabilityAsync(assetCode, availabilityPct, actor, cancellationToken);
        return RedirectToAction(nameof(Index), new { select = assetCode });
    }
}
