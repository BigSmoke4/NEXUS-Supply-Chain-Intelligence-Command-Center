using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Forecasting.Application;

namespace NEXUS.Modules.Forecasting.Presentation;

[ApiController]
[Route("api/forecasts")]
public sealed class ForecastsApiController : ControllerBase
{
    private readonly IForecastingService _forecastingService;

    public ForecastsApiController(IForecastingService forecastingService)
    {
        _forecastingService = forecastingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetForecasts(CancellationToken cancellationToken = default)
    {
        var predictions = await _forecastingService.GetLatestPredictionsAsync(cancellationToken);
        var horizon = await _forecastingService.GetDemandForecastHorizonAsync(14, cancellationToken);
        return Ok(new
        {
            predictions,
            horizon
        });
    }

    [HttpPost("predict")]
    public async Task<IActionResult> RunPrediction([FromBody] RunPredictionRequestDto request, CancellationToken cancellationToken = default)
    {
        var prediction = await _forecastingService.RunPredictionPipelineAsync(request, cancellationToken);
        return Ok(prediction);
    }
}
