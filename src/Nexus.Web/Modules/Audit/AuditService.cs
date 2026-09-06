using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.Audit;

public interface IAuditService
{
    Task RecordAsync(
        Guid organizationId, string? userId, string action, string entityType, Guid entityId,
        string? beforeState = null, string? afterState = null,
        string? aiAgent = null, double? aiConfidence = null, int? aiEvidenceCount = null,
        CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly NexusDbContext _db;
    public AuditService(NexusDbContext db) => _db = db;

    public async Task RecordAsync(
        Guid organizationId, string? userId, string action, string entityType, Guid entityId,
        string? beforeState = null, string? afterState = null,
        string? aiAgent = null, double? aiConfidence = null, int? aiEvidenceCount = null,
        CancellationToken ct = default)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            OrganizationId = organizationId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeState = beforeState,
            AfterState = afterState,
            AiAgent = aiAgent,
            AiConfidence = aiConfidence,
            AiEvidenceCount = aiEvidenceCount
        });
        // Audit writes are saved in the same transaction as the caller's
        // SaveChanges when possible; SaveChangesAsync here covers callers
        // (like background jobs) that don't already have a pending save.
        await _db.SaveChangesAsync(ct);
    }
}
