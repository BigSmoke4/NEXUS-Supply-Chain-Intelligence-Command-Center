using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

public interface IIdentityService
{
    Task<IReadOnlyList<NexusUser>> GetAllUsersAsync(CancellationToken cancellationToken = default);
}
