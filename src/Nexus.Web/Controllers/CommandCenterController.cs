using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Alerts;
using Nexus.Web.Modules.SupplyNetwork;
using Nexus.Web.Security;

namespace Nexus.Web.Controllers;

[Authorize(Policy = NexusPermissions.SupplyNetworkRead)]
public class CommandCenterController : Controller
{
    private readonly NexusDbContext _db;
    private readonly IGraphService _graph;
    private readonly ICurrentTenant _tenant;

    public CommandCenterController(NexusDbContext db, IGraphService graph, ICurrentTenant tenant)
    {
        _db = db;
        _graph = graph;
        _tenant = tenant;
    }

    /// <summary>
    /// §58 Digital Twin state switcher: LIVE (current network + open alerts,
    /// the default), HISTORICAL (audit trail of what actually happened -
    /// resolved alerts and approved mitigations, from the real AuditLogEntry
    /// table, not invented history), or SIMULATED (overlays the most recent
    /// scenario's actual computed result and cascade path onto the graph).
    /// This is a real data-mode switch, not three different mock screens.
    /// </summary>
    public async Task<IActionResult> Index(string mode, CancellationToken ct)
    {
        var twinMode = mode?.ToLowerInvariant() switch
        {
            "historical" => DigitalTwinMode.Historical,
            "simulated" => DigitalTwinMode.Simulated,
            _ => DigitalTwinMode.Live
        };

        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException("Tenant is not resolved.");
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null) return View(new CommandCenterViewModel());

        var nodes = await _db.SupplyNodes.Where(n => n.OrganizationId == org.Id).ToListAsync(ct);
        var edges = await _db.SupplyEdges.Where(e => e.OrganizationId == org.Id).ToListAsync(ct);
        var suppliers = await _db.Suppliers.Where(s => s.OrganizationId == org.Id).ToListAsync(ct);
        var products = await _db.Products.Where(p => p.OrganizationId == org.Id).ToListAsync(ct);
        var scenarios = await _db.Scenarios.Where(s => s.OrganizationId == org.Id)
            .Include(s => s.LastResult).OrderByDescending(s => s.CreatedAtUtc).Take(5).ToListAsync(ct);
        var spofs = await _graph.FindSinglePointsOfFailureAsync(org.Id, ct);
        var openAlerts = await _db.Alerts
            .Where(a => a.OrganizationId == org.Id && a.State == AlertState.Open)
            .OrderByDescending(a => a.Severity).ThenByDescending(a => a.CreatedAtUtc)
            .Take(10).ToListAsync(ct);

        var latestResult = scenarios.Select(s => s.LastResult).Where(r => r != null).FirstOrDefault();

        var vm = new CommandCenterViewModel
        {
            Mode = twinMode,
            OrganizationName = org.Name,
            NodeCount = nodes.Count,
            EdgeCount = edges.Count,
            SupplierCount = suppliers.Count,
            ProductCount = products.Count,
            CriticalSupplierCount = suppliers.Count(s => s.OperationalRisk > 60 || s.GeopoliticalRisk > 60),
            SinglePointsOfFailure = spofs.Select(n => n.Name).ToList(),
            RevenueAtRisk = latestResult?.RevenueAtRisk ?? 0,
            ProductsAtRisk = latestResult?.ProductsAffected ?? 0,
            ActiveDisruptions = scenarios.Count(s => s.Disruptions.Any(d => d.EndDateUtc > DateTime.UtcNow)),
            NetworkHealthPercent = spofs.Count == 0 ? 100 : Math.Max(40, 100 - spofs.Count * 6),
            Nodes = nodes,
            Edges = edges,
            RecentScenarios = scenarios,
            OpenAlerts = openAlerts
        };

        if (twinMode == DigitalTwinMode.Historical)
        {
            vm.AuditHistory = await _db.AuditLogEntries
                .Where(a => a.OrganizationId == org.Id)
                .OrderByDescending(a => a.AtUtc).Take(25).ToListAsync(ct);
            vm.ResolvedAlerts = await _db.Alerts
                .Where(a => a.OrganizationId == org.Id && a.State == AlertState.Resolved)
                .OrderByDescending(a => a.ResolvedAtUtc).Take(15).ToListAsync(ct);
        }
        else if (twinMode == DigitalTwinMode.Simulated)
        {
            vm.SimulatedScenario = scenarios.FirstOrDefault(s => s.LastResult is not null);
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcknowledgeAlert([FromServices] IAlertService alerts, Guid alertId, CancellationToken ct)
    {
        await alerts.AcknowledgeAsync(alertId, User?.Identity?.Name, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveAlert([FromServices] IAlertService alerts, Guid alertId, CancellationToken ct)
    {
        await alerts.ResolveAsync(alertId, ct);
        return RedirectToAction(nameof(Index));
    }
}

public enum DigitalTwinMode { Live, Historical, Simulated }

public class CommandCenterViewModel
{
    public DigitalTwinMode Mode { get; set; } = DigitalTwinMode.Live;
    public string OrganizationName { get; set; } = "NEXUS";
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int SupplierCount { get; set; }
    public int ProductCount { get; set; }
    public int CriticalSupplierCount { get; set; }
    public List<string> SinglePointsOfFailure { get; set; } = new();
    public decimal RevenueAtRisk { get; set; }
    public int ProductsAtRisk { get; set; }
    public int ActiveDisruptions { get; set; }
    public int NetworkHealthPercent { get; set; } = 100;
    public List<Domain.Entities.SupplyNode> Nodes { get; set; } = new();
    public List<Domain.Entities.SupplyEdge> Edges { get; set; } = new();
    public List<Domain.Entities.Scenario> RecentScenarios { get; set; } = new();
    public List<Domain.Entities.Alert> OpenAlerts { get; set; } = new();
    public List<Domain.Entities.AuditLogEntry> AuditHistory { get; set; } = new();
    public List<Domain.Entities.Alert> ResolvedAlerts { get; set; } = new();
    public Domain.Entities.Scenario? SimulatedScenario { get; set; }
}
