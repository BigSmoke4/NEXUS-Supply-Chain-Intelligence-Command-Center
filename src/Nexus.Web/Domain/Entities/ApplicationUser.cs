using Microsoft.AspNetCore.Identity;

namespace Nexus.Web.Domain.Entities;

// Extends Identity's user with tenant + role context (§22 multi-tenancy, §23 RBAC).
public class ApplicationUser : IdentityUser
{
    public Guid OrganizationId { get; set; }
    public string DisplayName { get; set; } = default!;
}

public static class NexusRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string SupplyChainExecutive = "SupplyChainExecutive";
    public const string SupplyChainAnalyst = "SupplyChainAnalyst";
    public const string ProcurementManager = "ProcurementManager";
    public const string LogisticsManager = "LogisticsManager";
    public const string RiskManager = "RiskManager";
    public const string OperationsManager = "OperationsManager";
    public const string AISupervisor = "AISupervisor";
    public const string Viewer = "Viewer";

    public static readonly string[] All =
    {
        SystemAdministrator, SupplyChainExecutive, SupplyChainAnalyst, ProcurementManager,
        LogisticsManager, RiskManager, OperationsManager, AISupervisor, Viewer
    };
}

public static class NexusPermissions
{
    public const string SupplyNetworkRead = "SupplyNetwork.Read";
    public const string SupplyNetworkWrite = "SupplyNetwork.Write";
    public const string SimulationRead = "Simulation.Read";
    public const string SimulationCreate = "Simulation.Create";
    public const string MitigationRead = "Mitigation.Read";
    public const string MitigationApprove = "Mitigation.Approve";
    public const string AIExecute = "AI.Execute";
    public const string AIApprove = "AI.Approve";
    public const string ReportsRead = "Reports.Read";
}
