using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Execution.Application;
using NEXUS.Modules.Execution.Domain;

namespace NEXUS.Modules.Execution.Presentation;

public sealed record ExecutionViewModel(
    Execution ActiveExecution,
    IReadOnlyList<Execution> Executions);

public sealed class ExecutionController : Controller
{
    private readonly IExecutionService _executionService;

    public ExecutionController(IExecutionService executionService)
    {
        _executionService = executionService;
    }

    [HttpGet("/Execution")]
    public async Task<IActionResult> Index([FromQuery] Guid? id = null, CancellationToken cancellationToken = default)
    {
        var list = await _executionService.GetExecutionsAsync(25, cancellationToken);
        var active = id.HasValue
            ? await _executionService.GetExecutionByIdAsync(id.Value, cancellationToken) ?? list.First()
            : list.First();

        return View("~/Views/Execution/Index.cshtml", new ExecutionViewModel(active, list));
    }
}
