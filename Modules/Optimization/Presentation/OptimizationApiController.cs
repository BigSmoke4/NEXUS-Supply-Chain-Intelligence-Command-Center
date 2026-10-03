using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Optimization.Application;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Optimization.Presentation;

public sealed record MitigationBudgetRequest(decimal BudgetLimitUsd = 2_800_000m);

[ApiController]
[Route("api/optimization")]
public sealed class OptimizationApiController : ControllerBase
{
    private readonly IOptimizationService _optimizationService;

    public OptimizationApiController(IOptimizationService optimizationService)
    {
        _optimizationService = optimizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var list = await _optimizationService.GetOptimizationRunsAsync(20, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _optimizationService.GetOptimizationRunByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiErrorResponse("OPTIMIZATION_NOT_FOUND", $"Optimization run {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> RunOptimization([FromBody] RunOptimizationCommand command, CancellationToken cancellationToken = default)
    {
        var run = await _optimizationService.ExecuteMultiObjectiveOptimizationAsync(command, null, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = run.Id }, run);
    }

    [HttpPost("mitigation-portfolio")]
    public async Task<IActionResult> OptimizeMitigations([FromBody] MitigationBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _optimizationService.OptimizeMitigationPortfolioAsync(request.BudgetLimitUsd, cancellationToken);
        return Ok(result);
    }
}
