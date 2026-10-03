using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Inventory.Application;

namespace NEXUS.Modules.Inventory.Presentation;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryApiController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryApiController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("snapshots")]
    public async Task<IActionResult> GetSnapshots(CancellationToken cancellationToken)
    {
        var items = await _inventoryService.GetLatestSnapshotsAsync(cancellationToken);
        return Ok(items);
    }
}
