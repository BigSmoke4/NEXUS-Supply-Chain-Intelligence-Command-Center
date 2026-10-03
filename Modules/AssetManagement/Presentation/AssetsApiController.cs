using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.AssetManagement.Application;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.AssetManagement.Presentation;

public sealed record UpdateAssetCapacityRequest(decimal AvailabilityPct);

[ApiController]
[Route("api/assets")]
public sealed class AssetsApiController : ControllerBase
{
    private readonly IAssetManagementService _assetService;

    public AssetsApiController(IAssetManagementService assetService)
    {
        _assetService = assetService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssets(
        [FromQuery] string? type = null,
        [FromQuery] string? region = null,
        [FromQuery] string? search = null,
        [FromQuery] bool highRisk = false,
        [FromQuery] bool bottlenecks = false,
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
    {
        var items = await _assetService.GetAssetsAsync(type, region, search, highRisk, bottlenecks, limit, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAssetById(string id, CancellationToken cancellationToken = default)
    {
        var item = await _assetService.GetAssetByIdOrCodeAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiErrorResponse("ASSET_NOT_FOUND", $"Asset '{id}' was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(item);
    }

    [HttpPost("{id}/capacity")]
    public async Task<IActionResult> UpdateCapacity(string id, [FromBody] UpdateAssetCapacityRequest request, CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var updated = await _assetService.UpdateAssetCapacityAvailabilityAsync(id, request.AvailabilityPct, actor, cancellationToken);
        if (updated is null)
        {
            return NotFound(new ApiErrorResponse("ASSET_NOT_FOUND", $"Asset '{id}' was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(updated);
    }
}
