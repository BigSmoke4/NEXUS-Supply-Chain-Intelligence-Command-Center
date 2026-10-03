using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.Organization.Infrastructure;

public sealed class OrganizationService : IOrganizationService
{
    private readonly NexusDbContext _db;

    public OrganizationService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<EnterpriseOrganization> GetCurrentOrganizationAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? new EnterpriseOrganization();
    }
}
