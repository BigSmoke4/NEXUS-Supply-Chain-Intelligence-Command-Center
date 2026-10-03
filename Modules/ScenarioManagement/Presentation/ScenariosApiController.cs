using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.ScenarioManagement.Presentation;

[ApiController]
[Route("api/scenarios")]
public sealed class ScenariosApiController : ControllerBase
{
    private readonly IScenarioService _scenarioService;

    public ScenariosApiController(IScenarioService scenarioService)
    {
        _scenarioService = scenarioService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var list = await _scenarioService.GetScenariosAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _scenarioService.GetScenarioByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ApiErrorResponse("SCENARIO_NOT_FOUND", $"Scenario {id} was not found.", HttpContext.TraceIdentifier, DateTime.UtcNow));
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScenarioCommand command, CancellationToken cancellationToken = default)
    {
        var created = await _scenarioService.CreateScenarioAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
