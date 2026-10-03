using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class IdentityService : IIdentityService
{
    private readonly NexusDbContext _db;

    public IdentityService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NexusUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);
    }
}
