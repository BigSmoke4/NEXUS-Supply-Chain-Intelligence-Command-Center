using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Reporting;

namespace Nexus.Web.Controllers;

[Authorize(Policy = NexusPermissions.ReportsRead)]
public class ReportsController : Controller
{
    private readonly NexusDbContext _db;
    private readonly IExecutiveSummaryReportGenerator _reportGenerator;

    public ReportsController(NexusDbContext db, IExecutiveSummaryReportGenerator reportGenerator)
    {
        _db = db;
        _reportGenerator = reportGenerator;
    }

    [HttpGet]
    public async Task<IActionResult> ExecutiveSummary(Guid scenarioId, CancellationToken ct)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.LastResult)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, ct);

        if (scenario?.LastResult is null) return NotFound("No computed result to report on.");

        var pdfBytes = _reportGenerator.Generate(scenario);
        var fileName = $"NEXUS-Executive-Summary-{scenario.Name.Replace(" ", "-")}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}
