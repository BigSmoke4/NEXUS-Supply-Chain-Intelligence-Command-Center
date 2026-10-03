using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Forecasting.Application;
using NEXUS.Modules.Forecasting.Domain;

namespace NEXUS.Modules.Forecasting.Presentation;

public sealed record ForecastingViewModel(
    IReadOnlyList<Prediction> Predictions,
    IReadOnlyList<Forecast> DemandForecasts);

public sealed class ForecastingController : Controller
{
    private readonly IForecastingService _forecastingService;

    public ForecastingController(IForecastingService forecastingService)
    {
        _forecastingService = forecastingService;
    }

    [HttpGet("/Forecasting")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var predictions = await _forecastingService.GetLatestPredictionsAsync(cancellationToken);
        var forecasts = await _forecastingService.GetDemandForecastHorizonAsync(14, cancellationToken);
        return View("~/Views/Forecasting/Index.cshtml", new ForecastingViewModel(predictions, forecasts));
    }

    [HttpPost("/Forecasting/Predict")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Predict(
        string targetDomain,
        string targetAssetCode,
        int horizonDays = 14,
        decimal utilizationPct = 87m,
        decimal supplierReliabilityPct = 91m,
        decimal inventoryCoverageDays = 19m,
        decimal demandGrowthPct = 8m,
        CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Analyst";
        await _forecastingService.RunPredictionPipelineAsync(
            new RunPredictionRequestDto(
                targetDomain,
                targetAssetCode,
                horizonDays,
                utilizationPct,
                supplierReliabilityPct,
                inventoryCoverageDays,
                demandGrowthPct,
                actor),
            cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
