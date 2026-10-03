using NEXUS.Modules.Audit.Domain;

namespace NEXUS.Modules.Audit.Application;

public interface IAuditService
{
    Task RecordAsync(
        string actorName,
        string actorRole,
        string moduleName,
        string actionType,
        string entityType,
        string entityId,
        string summary,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50, CancellationToken cancellationToken = default);
}
