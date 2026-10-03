using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace NEXUS.Modules.Identity.Domain;

public static class NexusRoles
{
    public const string Administrator = "Administrator";
    public const string Executive = "Executive";
    public const string OperationsManager = "OperationsManager";
    public const string SupplyChainManager = "SupplyChainManager";
    public const string RiskManager = "RiskManager";
    public const string Analyst = "Analyst";
    public const string DecisionApprover = "DecisionApprover";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Administrator,
        Executive,
        OperationsManager,
        SupplyChainManager,
        RiskManager,
        Analyst,
        DecisionApprover,
        Viewer
    };
}

public sealed class NexusUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }

    [MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Department { get; set; } = "Global Operations";

    [MaxLength(80)]
    public string PrimaryRoleTitle { get; set; } = NexusRoles.Viewer;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}

public sealed class NexusRole : IdentityRole<Guid>
{
    [MaxLength(250)]
    public string Description { get; set; } = string.Empty;
}
