using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.SupplyChain.Application;

namespace NEXUS.Modules.SupplyChain.Presentation;

[ApiController]
[Route("api/supply-chain")]
public sealed class SupplyChainApiController : ControllerBase
{
    private readonly ISupplyChainService _supplyChainService;

    public SupplyChainApiController(ISupplyChainService supplyChainService)
    {
        _supplyChainService = supplyChainService;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await _supplyChainService.GetOverviewAsync(cancellationToken);
        return Ok(overview);
    }
}
