using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Audit.Domain;

public sealed class AuditLog : EntityBase
{
    public Guid? ActorUserId { get; set; }

    [Required, MaxLength(160)]
    public string ActorName { get; set; } = "SYSTEM";

    [MaxLength(80)]
    public string ActorRole { get; set; } = "System";

    [Required, MaxLength(80)]
    public string ModuleName { get; set; } = "Core";

    [Required, MaxLength(120)]
    public string ActionType { get; set; } = string.Empty;

    [MaxLength(80)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(80)]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Summary { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
