using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Controllers;

[Authorize(Policy = NexusPermissions.SimulationRead)]
public class ScenarioComparisonController : Controller
{
    private readonly NexusDbContext _db;
    public ScenarioComparisonController(NexusDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] Guid[] ids, CancellationToken ct)
    {
        var org = await _db.Organizations.FirstAsync(ct);

        if (ids is null || ids.Length == 0)
        {
            // No selection yet: show a picker of every scenario that has a
            // computed result, so the user can choose 2-3 to compare (§12).
            var available = await _db.Scenarios
                .Where(s => s.OrganizationId == org.Id && s.LastResult != null)
                .Include(s => s.LastResult)
                .OrderByDescending(s => s.CreatedAtUtc)
                .ToListAsync(ct);
            return View("Picker", available);
        }

        var scenarios = await _db.Scenarios
            .Where(s => ids.Contains(s.Id))
            .Include(s => s.Disruptions)
            .Include(s => s.LastResult)
            .ToListAsync(ct);

        return View("Compare", scenarios);
    }
}
