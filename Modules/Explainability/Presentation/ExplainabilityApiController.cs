using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Explainability.Application;

namespace NEXUS.Modules.Explainability.Presentation;

[ApiController]
[Route("api/explainability")]
public sealed class ExplainabilityApiController : ControllerBase
{
    private readonly IExplainabilityService _explainabilityService;

    public ExplainabilityApiController(IExplainabilityService explainabilityService)
    {
        _explainabilityService = explainabilityService;
    }

    [HttpGet("{decisionId:guid}")]
    public async Task<IActionResult> GetExplanation(Guid decisionId, CancellationToken cancellationToken)
    {
        var exp = await _explainabilityService.GetExplanationForDecisionAsync(decisionId, cancellationToken);
        return exp is null ? NotFound() : Ok(exp);
    }
}
