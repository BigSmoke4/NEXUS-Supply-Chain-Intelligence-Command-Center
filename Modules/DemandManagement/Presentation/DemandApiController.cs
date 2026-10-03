using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DemandManagement.Application;

namespace NEXUS.Modules.DemandManagement.Presentation;

[ApiController]
[Route("api/demand")]
public sealed class DemandApiController : ControllerBase
{
    private readonly IDemandService _demandService;

    public DemandApiController(IDemandService demandService)
    {
        _demandService = demandService;
    }

    [HttpGet("snapshots")]
    public async Task<IActionResult> GetSnapshots(CancellationToken cancellationToken)
    {
        var items = await _demandService.GetLatestDemandSnapshotsAsync(cancellationToken);
        return Ok(items);
    }
}
