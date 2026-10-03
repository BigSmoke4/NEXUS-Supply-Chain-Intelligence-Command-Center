using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Simulation.Application;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Simulation.Presentation;

[ApiController]
[Route("api/simulations")]
public sealed class SimulationsApiController : ControllerBase
{
    private readonly ISimulationService _simulationService;

    public SimulationsApiController(ISimulationService simulationService)
    {
        _simulationService = simulationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var list = await _simulationService.GetSimulationRunsAsync(25, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _simulationService.GetSimulationRunByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiErrorResponse("SIMULATION_NOT_FOUND", $"Simulation run {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> RunSimulation([FromBody] RunSimulationCommand command, CancellationToken cancellationToken = default)
    {
        var run = await _simulationService.ExecuteSimulationAsync(command, null, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = run.Id }, run);
    }
}
