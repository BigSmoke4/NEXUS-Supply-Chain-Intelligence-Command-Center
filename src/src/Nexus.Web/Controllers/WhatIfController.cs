using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.WhatIf;
using Nexus.Web.Security;

namespace Nexus.Web.Controllers;

[Authorize(Policy = NexusPermissions.SimulationRead)]
public class WhatIfController : Controller
{
    private readonly NexusDbContext _db;
    private readonly IWhatIfEngine _engine;
    private readonly ICurrentTenant _tenant;

    public WhatIfController(NexusDbContext db, IWhatIfEngine engine, ICurrentTenant tenant)
    {
        _db = db;
        _engine = engine;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
        ViewBag.Nodes = await _db.SupplyNodes
            .Where(n => n.OrganizationId == orgId
                && (n.Type == NodeType.Supplier || n.Type == NodeType.Factory || n.Type == NodeType.Warehouse))
            .OrderBy(n => n.Name).ToListAsync(ct);
        return View();
    }

    /// Instant recalculation endpoint (§57) - called on every slider move.
    /// Read-only, no persistence: What-If exploration never creates a
    /// Scenario/Disruption record, keeping it clearly separate from the
    /// authoritative simulation path.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(Guid nodeId, double capacityPercent, double additionalInventoryDays, double demandMultiplierPercent, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
        var result = await _engine.CalculateAsync(orgId,
            new WhatIfInput(nodeId, capacityPercent, additionalInventoryDays, demandMultiplierPercent), ct);

        return Json(new
        {
            revenueAtRisk = result.EstimatedRevenueAtRisk,
            affectedProducts = result.AffectedProductCount,
            stockoutDays = result.EstimatedStockoutDays,
            serviceLevel = result.EstimatedServiceLevelPercent,
            additionalCost = result.EstimatedAdditionalCost
        });
    }
}
