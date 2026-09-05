using Microsoft.AspNetCore.Authorization;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Security;

/// <summary>
/// Maps each granular NexusPermissions constant to the roles allowed to hold
/// it (§23). Registered once in Program.cs via AddAuthorizationPolicies();
/// controllers then declare [Authorize(Policy = NexusPermissions.X)] and
/// never hard-code role checks inline.
/// </summary>
public static class AuthorizationPolicyRegistration
{
    private static readonly Dictionary<string, string[]> PermissionToRoles = new()
    {
        [NexusPermissions.SupplyNetworkRead] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainExecutive, NexusRoles.SupplyChainAnalyst,
            NexusRoles.ProcurementManager, NexusRoles.LogisticsManager, NexusRoles.RiskManager,
            NexusRoles.OperationsManager, NexusRoles.AISupervisor, NexusRoles.Viewer
        },
        [NexusPermissions.SupplyNetworkWrite] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainAnalyst, NexusRoles.ProcurementManager
        },
        [NexusPermissions.SimulationRead] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainExecutive, NexusRoles.SupplyChainAnalyst,
            NexusRoles.RiskManager, NexusRoles.OperationsManager, NexusRoles.Viewer
        },
        [NexusPermissions.SimulationCreate] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainAnalyst, NexusRoles.RiskManager,
            NexusRoles.OperationsManager
        },
        [NexusPermissions.MitigationRead] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainExecutive, NexusRoles.SupplyChainAnalyst,
            NexusRoles.RiskManager, NexusRoles.OperationsManager, NexusRoles.Viewer
        },
        [NexusPermissions.MitigationApprove] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainExecutive, NexusRoles.OperationsManager
        },
        [NexusPermissions.AIExecute] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainAnalyst, NexusRoles.AISupervisor,
            NexusRoles.RiskManager
        },
        [NexusPermissions.AIApprove] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.AISupervisor
        },
        [NexusPermissions.ReportsRead] = new[]
        {
            NexusRoles.SystemAdministrator, NexusRoles.SupplyChainExecutive, NexusRoles.Viewer
        },
    };

    public static void AddNexusAuthorizationPolicies(this AuthorizationOptions options)
    {
        foreach (var (permission, roles) in PermissionToRoles)
        {
            options.AddPolicy(permission, policy => policy.RequireRole(roles));
        }
    }
}
