using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Risk;

namespace Nexus.Web.Controllers;

[Authorize(Policy = NexusPermissions.SupplyNetworkRead)]
public class SuppliersController : Controller
{
    private readonly NexusDbContext _db;
    private readonly IRiskScoringService _risk;

    public SuppliersController(NexusDbContext db, IRiskScoringService risk)
    {
        _db = db;
        _risk = risk;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var suppliers = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync(ct);
        var vm = suppliers.Select(s => new SupplierRowViewModel
        {
            Supplier = s,
            RiskScore = _risk.CalculateSupplierRiskScore(s)
        }).OrderByDescending(v => v.RiskScore).ToList();

        return View(vm);
    }
}

public class SupplierRowViewModel
{
    public Domain.Entities.Supplier Supplier { get; set; } = default!;
    public double RiskScore { get; set; }
}
