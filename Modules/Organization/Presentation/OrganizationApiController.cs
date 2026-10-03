using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Presentation;

[ApiController]
[Route("api/organization")]
public sealed class OrganizationApiController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationApiController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrganization(CancellationToken cancellationToken)
    {
        var org = await _organizationService.GetCurrentOrganizationAsync(cancellationToken);
        return Ok(org);
    }
}
