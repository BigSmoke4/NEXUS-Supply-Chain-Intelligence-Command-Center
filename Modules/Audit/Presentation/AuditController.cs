using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Audit.Application;

namespace NEXUS.Modules.Audit.Presentation;

public sealed class AuditController : Controller
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("/Audit")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var logs = await _auditService.GetRecentLogsAsync(100, cancellationToken);
        return View("~/Views/Audit/Index.cshtml", logs);
    }
}
