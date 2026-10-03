using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DependencyGraph.Application;

namespace NEXUS.Modules.DependencyGraph.Presentation;

public sealed record DependencyGraphViewModel(
    EnterpriseGraphDto Graph,
    DependencyImpactAnalysisDto Impact);

public sealed class DependencyGraphController : Controller
{
    private readonly IDependencyGraphService _graphService;

    public DependencyGraphController(IDependencyGraphService graphService)
    {
        _graphService = graphService;
    }

    [HttpGet("/DependencyGraph")]
    public async Task<IActionResult> Index(
        [FromQuery] string asset = "SUP-001",
        [FromQuery] decimal reductionPct = 40m,
        [FromQuery] int durationDays = 14,
        CancellationToken cancellationToken = default)
    {
        var graph = await _graphService.GetEnterpriseGraphAsync(180, null, cancellationToken);
        var impact = await _graphService.AnalyzeImpactAsync(asset, reductionPct, durationDays, cancellationToken);
        return View("~/Views/DependencyGraph/Index.cshtml", new DependencyGraphViewModel(graph, impact));
    }
}
