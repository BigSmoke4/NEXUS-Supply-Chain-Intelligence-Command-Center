using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DependencyGraph.Application;

namespace NEXUS.Modules.DependencyGraph.Presentation;

[ApiController]
[Route("api/dependency-graph")]
public sealed class DependencyGraphApiController : ControllerBase
{
    private readonly IDependencyGraphService _graphService;

    public DependencyGraphApiController(IDependencyGraphService graphService)
    {
        _graphService = graphService;
    }

    [HttpGet]
    public async Task<IActionResult> GetGraph(
        [FromQuery] int maxNodes = 160,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await _graphService.GetEnterpriseGraphAsync(maxNodes, type, cancellationToken);
        return Ok(graph);
    }

    [HttpGet("{id}/impact")]
    public async Task<IActionResult> GetImpact(
        string id,
        [FromQuery] decimal reductionPct = 40m,
        [FromQuery] int durationDays = 14,
        CancellationToken cancellationToken = default)
    {
        var impact = await _graphService.AnalyzeImpactAsync(id, reductionPct, durationDays, cancellationToken);
        return Ok(impact);
    }
}
