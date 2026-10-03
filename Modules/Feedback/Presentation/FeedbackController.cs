using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.Feedback.Application;

namespace NEXUS.Modules.Feedback.Presentation;

public sealed record FeedbackViewModel(
    IReadOnlyList<DecisionFeedbackPairDto> FeedbackRecords,
    IReadOnlyList<Decision> Decisions);

public sealed class FeedbackController : Controller
{
    private readonly IFeedbackService _feedbackService;
    private readonly IDecisionEngineService _decisionService;

    public FeedbackController(
        IFeedbackService feedbackService,
        IDecisionEngineService decisionService)
    {
        _feedbackService = feedbackService;
        _decisionService = decisionService;
    }

    [HttpGet("/Feedback")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var feedback = await _feedbackService.GetFeedbackHistoryAsync(25, cancellationToken);
        var decisions = await _decisionService.GetDecisionsAsync(25, cancellationToken);
        return View("~/Views/Feedback/Index.cshtml", new FeedbackViewModel(feedback, decisions));
    }

    [HttpPost("/Feedback/RecordOutcome")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordOutcome(
        Guid decisionId,
        decimal predictedRevenueLossUsd = 420_000m,
        decimal actualRevenueLossUsd = 390_000m,
        decimal predictedAdditionalCostUsd = 420_000m,
        decimal actualAdditionalCostUsd = 408_500m,
        decimal predictedServiceLevelPct = 98.2m,
        decimal actualServiceLevelPct = 98.5m,
        decimal predictedRecoveryDays = 5.0m,
        decimal actualRecoveryDays = 4.6m,
        CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        await _feedbackService.RecordOutcomeAndEvaluateQualityAsync(
            new RecordDecisionOutcomeCommand(
                decisionId,
                null,
                predictedRevenueLossUsd,
                actualRevenueLossUsd,
                predictedAdditionalCostUsd,
                actualAdditionalCostUsd,
                predictedServiceLevelPct,
                actualServiceLevelPct,
                predictedRecoveryDays,
                actualRecoveryDays,
                actor),
            cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
