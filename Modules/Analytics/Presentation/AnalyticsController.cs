using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Analytics.Application;
using NEXUS.Modules.Analytics.Domain;
using NEXUS.Modules.Optimization.Application;

namespace NEXUS.Modules.Analytics.Presentation;

public sealed record AnalyticsViewModel(
    ResilienceScore Resilience,
    BlackSwanStressResultDto BlackSwanResult,
    MitigationPortfolioResultDto MitigationPortfolio);

public sealed class AnalyticsController : Controller
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IOptimizationService _optimizationService;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        IOptimizationService optimizationService)
    {
        _analyticsService = analyticsService;
        _optimizationService = optimizationService;
    }

    [HttpGet("/Analytics")]
    public async Task<IActionResult> Index(
        [FromQuery] decimal supDelta = -40m,
        [FromQuery] decimal demDelta = 35m,
        [FromQuery] decimal trnDelta = -20m,
        [FromQuery] decimal engDelta = 25m,
        [FromQuery] decimal facDelta = -15m,
        [FromQuery] decimal budgetUsd = 2_800_000m,
        CancellationToken cancellationToken = default)
    {
        var resilience = await _analyticsService.GetOrCalculateResilienceScoreAsync(false, cancellationToken);
        var blackSwan = await _analyticsService.RunBlackSwanStressTestAsync(
            new BlackSwanStressRequestDto(supDelta, demDelta, trnDelta, engDelta, facDelta, 2500, User.Identity?.Name ?? "Risk Manager"),
            cancellationToken);
        var portfolio = await _optimizationService.OptimizeMitigationPortfolioAsync(budgetUsd, cancellationToken);

        return View("~/Views/Analytics/Index.cshtml", new AnalyticsViewModel(resilience, blackSwan, portfolio));
    }
}
