using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Analytics.Application;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Modules.Feedback.Application;
using NEXUS.Modules.Optimization.Application;

namespace NEXUS.Modules.Analytics.Presentation;

[ApiController]
[Route("api")]
public sealed class AnalyticsApiController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IDigitalTwinService _digitalTwinService;
    private readonly IOptimizationService _optimizationService;
    private readonly IFeedbackService _feedbackService;

    public AnalyticsApiController(
        IAnalyticsService analyticsService,
        IDigitalTwinService digitalTwinService,
        IOptimizationService optimizationService,
        IFeedbackService feedbackService)
    {
        _analyticsService = analyticsService;
        _digitalTwinService = digitalTwinService;
        _optimizationService = optimizationService;
        _feedbackService = feedbackService;
    }

    [HttpGet("resilience")]
    public async Task<IActionResult> GetResilience([FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        var score = await _analyticsService.GetOrCalculateResilienceScoreAsync(refresh, cancellationToken);
        return Ok(score);
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalyticsOverview(CancellationToken cancellationToken = default)
    {
        var state = await _digitalTwinService.GetCurrentEnterpriseStateAsync(false, cancellationToken);
        var resilience = await _analyticsService.GetOrCalculateResilienceScoreAsync(false, cancellationToken);
        var portfolio = await _optimizationService.OptimizeMitigationPortfolioAsync(2_800_000m, cancellationToken);
        var feedback = await _feedbackService.GetFeedbackHistoryAsync(10, cancellationToken);

        return Ok(new
        {
            state,
            resilience,
            portfolio,
            feedback
        });
    }

    [HttpPost("analytics/black-swan")]
    public async Task<IActionResult> RunBlackSwanStressTest([FromBody] BlackSwanStressRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await _analyticsService.RunBlackSwanStressTestAsync(request, cancellationToken);
        return Ok(result);
    }
}
