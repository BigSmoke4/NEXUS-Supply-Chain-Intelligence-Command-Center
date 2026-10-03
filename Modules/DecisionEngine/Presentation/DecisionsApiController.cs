using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.Execution.Application;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.DecisionEngine.Presentation;

public sealed record GenerateDecisionRequest(Guid ScenarioId);
public sealed record ReviewDecisionRequest(string? ReviewerName, string? ReviewerRole, string? Comments);
public sealed record AssistantQueryRequest(string Question);

[ApiController]
[Route("api/decisions")]
public sealed class DecisionsApiController : ControllerBase
{
    private readonly IDecisionEngineService _decisionService;
    private readonly IExecutionService _executionService;

    public DecisionsApiController(
        IDecisionEngineService decisionService,
        IExecutionService executionService)
    {
        _decisionService = decisionService;
        _executionService = executionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var items = await _decisionService.GetDecisionsAsync(25, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _decisionService.GetDecisionByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiErrorResponse("DECISION_NOT_FOUND", $"Decision {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var decision = await _decisionService.GenerateAutonomousDecisionAsync(request.ScenarioId, actor, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = decision.Id }, decision);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewDecisionRequest? request, CancellationToken cancellationToken = default)
    {
        var reviewer = request?.ReviewerName ?? User.Identity?.Name ?? "Marcus Vance (VP Global Operations)";
        var role = request?.ReviewerRole ?? "DecisionApprover";
        var comments = request?.Comments ?? "Approved via human-in-the-loop governance.";

        var updated = await _decisionService.ApproveDecisionAsync(id, reviewer, role, comments, cancellationToken);
        if (updated is null)
        {
            return NotFound(new ApiErrorResponse("DECISION_NOT_FOUND", $"Decision {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(updated);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewDecisionRequest? request, CancellationToken cancellationToken = default)
    {
        var reviewer = request?.ReviewerName ?? User.Identity?.Name ?? "Marcus Vance (VP Global Operations)";
        var role = request?.ReviewerRole ?? "DecisionApprover";
        var comments = request?.Comments ?? "Rejected via human-in-the-loop governance.";

        var updated = await _decisionService.RejectDecisionAsync(id, reviewer, role, comments, cancellationToken);
        if (updated is null)
        {
            return NotFound(new ApiErrorResponse("DECISION_NOT_FOUND", $"Decision {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(updated);
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var execution = await _executionService.ExecuteApprovedDecisionAsync(id, actor, cancellationToken);
        return Ok(execution);
    }

    [HttpPost("assistant")]
    public async Task<IActionResult> AskAssistant([FromBody] AssistantQueryRequest request, CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var response = await _decisionService.AskDecisionAssistantAsync(request.Question, actor, cancellationToken);
        return Ok(response);
    }
}
