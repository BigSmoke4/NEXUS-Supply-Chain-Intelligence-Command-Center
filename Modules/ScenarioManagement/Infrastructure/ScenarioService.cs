using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ScenarioManagement.Application;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.ScenarioManagement.Infrastructure;

public sealed class ScenarioService : IScenarioService
{
    private readonly NexusDbContext _db;
    private readonly INexusCacheService _cache;
    private readonly IAuditService _audit;

    public ScenarioService(
        NexusDbContext db,
        INexusCacheService cache,
        IAuditService audit)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IReadOnlyList<Scenario>> GetScenariosAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Scenarios
            .AsNoTracking()
            .Include(x => x.Parameters)
            .OrderByDescending(x => x.IsHeroScenario)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Scenario?> GetScenarioByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Scenarios
            .AsNoTracking()
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Scenario> CreateScenarioAsync(CreateScenarioCommand command, CancellationToken cancellationToken = default)
    {
        var targetCode = string.IsNullOrWhiteSpace(command.TargetAssetCode) ? "SUP-001" : command.TargetAssetCode.Trim().ToUpperInvariant();
        var asset = await _db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssetCode == targetCode, cancellationToken)
            ?? await _db.Assets.AsNoTracking().FirstAsync(cancellationToken);

        var count = await _db.Scenarios.CountAsync(cancellationToken) + 1;
        var scenario = new Scenario
        {
            Id = Guid.NewGuid(),
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            ScenarioCode = $"SCN-{count:D3}-{DateTime.UtcNow:HHmmss}",
            Name = string.IsNullOrWhiteSpace(command.Name)
                ? $"{command.ScenarioType} on {asset.AssetCode} ({command.DurationDays}d)"
                : command.Name.Trim(),
            ScenarioType = string.IsNullOrWhiteSpace(command.ScenarioType) ? ScenarioTypes.SupplierFailure : command.ScenarioType,
            Description = command.Description ?? $"What-If scenario evaluating {command.ScenarioType} on {asset.Name}: Supplier Capacity {command.SupplierCapacityMultiplierPct:F0}%, Demand {command.DemandDeltaPct:+0;-0;0}%, Transport Cost {command.TransportCostDeltaPct:+0;-0;0}% over {command.DurationDays} days.",
            TargetAssetId = asset.Id,
            TargetAssetCode = asset.AssetCode,
            TargetAssetName = asset.Name,
            DurationDays = Math.Clamp(command.DurationDays, 1, 180),
            SupplierCapacityMultiplierPct = Math.Clamp(command.SupplierCapacityMultiplierPct, 0m, 150m),
            DemandDeltaPct = Math.Clamp(command.DemandDeltaPct, -80m, 200m),
            TransportCostDeltaPct = Math.Clamp(command.TransportCostDeltaPct, -50m, 300m),
            FactoryCapacityDeltaPct = Math.Clamp(command.FactoryCapacityDeltaPct, -90m, 100m),
            EnergyCostDeltaPct = Math.Clamp(command.EnergyCostDeltaPct, -50m, 300m),
            IsBlackSwanScenario = command.IsBlackSwan,
            Status = "ACTIVE",
            CreatedByUser = command.CreatedByUser,
            Parameters = new List<ScenarioParameter>
            {
                new()
                {
                    OrganizationId = NexusSeedData.DefaultOrganizationId,
                    ParameterName = "Supplier Capacity",
                    TargetAssetCode = asset.AssetCode,
                    BaselineValue = 100m,
                    ScenarioValue = command.SupplierCapacityMultiplierPct,
                    DeltaPercent = command.SupplierCapacityMultiplierPct - 100m,
                    Unit = "%"
                },
                new()
                {
                    OrganizationId = NexusSeedData.DefaultOrganizationId,
                    ParameterName = "Demand",
                    TargetAssetCode = "MKT-001",
                    BaselineValue = 100m,
                    ScenarioValue = 100m + command.DemandDeltaPct,
                    DeltaPercent = command.DemandDeltaPct,
                    Unit = "%"
                },
                new()
                {
                    OrganizationId = NexusSeedData.DefaultOrganizationId,
                    ParameterName = "Transportation Cost",
                    TargetAssetCode = "TRN-001",
                    BaselineValue = 100m,
                    ScenarioValue = 100m + command.TransportCostDeltaPct,
                    DeltaPercent = command.TransportCostDeltaPct,
                    Unit = "%"
                }
            }
        };

        _db.Scenarios.Add(scenario);
        await _db.SaveChangesAsync(cancellationToken);

        await _cache.SetAsync(NexusCacheKeys.ScenarioTemp(scenario.Id), scenario, TimeSpan.FromMinutes(30), cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            command.CreatedByUser,
            "SupplyChainManager",
            "ScenarioManagement",
            "SCENARIO_CREATED",
            "Scenario",
            scenario.ScenarioCode,
            $"Created What-If Scenario '{scenario.Name}' targeting {scenario.TargetAssetCode}.");

        return scenario;
    }
}
