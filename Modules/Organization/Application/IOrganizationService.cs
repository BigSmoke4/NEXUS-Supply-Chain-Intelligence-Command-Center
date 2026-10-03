using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.Organization.Application;

public interface IOrganizationService
{
    Task<EnterpriseOrganization> GetCurrentOrganizationAsync(CancellationToken cancellationToken = default);
}
