using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.DigitalTwin.Application;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.DigitalTwin.Presentation;

[ApiController]
[Route("api")]
public sealed class EnterpriseApiController : ControllerBase
{
    private readonly IDigitalTwinService _digitalTwinService;
    private readonly IHeroDemonstrationService _heroService;
    private readonly IBackgroundJobService _jobService;

    public EnterpriseApiController(
        IDigitalTwinService digitalTwinService,
        IHeroDemonstrationService heroService,
        IBackgroundJobService jobService)
    {
        _digitalTwinService = digitalTwinService;
        _heroService = heroService;
        _jobService = jobService;
    }

    [HttpGet("enterprise/state")]
    public async Task<IActionResult> GetEnterpriseState([FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        var state = await _digitalTwinService.GetCurrentEnterpriseStateAsync(refresh, cancellationToken);
        return Ok(state);
    }

    [HttpPost("hero-demo/execute")]
    public async Task<IActionResult> ExecuteHeroDemo(CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name ?? "Operations Manager";
        var report = await _heroService.ExecuteFullHeroDemonstrationAsync(actor, cancellationToken);
        return Ok(report);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetRecentJobs(CancellationToken cancellationToken = default)
    {
        var jobs = await _jobService.GetRecentJobsAsync(20, cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJobStatus(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _jobService.GetStatusAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound(new ApiErrorResponse("JOB_NOT_FOUND", $"Background job {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(job);
    }
}
