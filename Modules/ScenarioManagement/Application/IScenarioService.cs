using NEXUS.Modules.ScenarioManagement.Domain;

namespace NEXUS.Modules.ScenarioManagement.Application;

public sealed record CreateScenarioCommand(
    string Name,
    string ScenarioType,
    string TargetAssetCode,
    int DurationDays,
    decimal SupplierCapacityMultiplierPct,
    decimal DemandDeltaPct,
    decimal TransportCostDeltaPct,
    decimal FactoryCapacityDeltaPct = 0m,
    decimal EnergyCostDeltaPct = 0m,
    string? Description = null,
    bool IsBlackSwan = false,
    string CreatedByUser = "Operations Manager");

public interface IScenarioService
{
    Task<IReadOnlyList<Scenario>> GetScenariosAsync(CancellationToken cancellationToken = default);
    Task<Scenario?> GetScenarioByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Scenario> CreateScenarioAsync(CreateScenarioCommand command, CancellationToken cancellationToken = default);
}
