using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Shared.Data;

namespace NEXUS.Modules.Audit.Infrastructure;

public sealed class AuditService : IAuditService
{
    private readonly NexusDbContext _db;

    public AuditService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        string actorName,
        string actorRole,
        string moduleName,
        string actionType,
        string entityType,
        string entityId,
        string summary,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "SYSTEM" : actorName,
            ActorRole = string.IsNullOrWhiteSpace(actorRole) ? "System" : actorRole,
            ModuleName = moduleName,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            MetadataJson = metadata is null ? "{}" : JsonSerializer.Serialize(metadata),
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        return await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(count, 1, 200))
            .ToListAsync(cancellationToken);
    }
}
