using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Analytics.Domain;
using NEXUS.Modules.AssetManagement.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.DemandManagement.Domain;
using NEXUS.Modules.DependencyGraph.Domain;
using NEXUS.Modules.DigitalTwin.Domain;
using NEXUS.Modules.Execution.Domain;
using NEXUS.Modules.Explainability.Domain;
using NEXUS.Modules.Feedback.Domain;
using NEXUS.Modules.Forecasting.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Inventory.Domain;
using NEXUS.Modules.Optimization.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Modules.Simulation.Domain;
using NEXUS.Modules.SupplyChain.Domain;

namespace NEXUS.Shared.Data;

public sealed class NexusDbContext : IdentityDbContext<NexusUser, NexusRole, Guid>
{
    public NexusDbContext(DbContextOptions<NexusDbContext> options)
        : base(options)
    {
    }

    public DbSet<EnterpriseOrganization> Organizations => Set<EnterpriseOrganization>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Factory> Factories => Set<Factory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<DistributionCenter> DistributionCenters => Set<DistributionCenter>();
    public DbSet<TransportationRoute> TransportationRoutes => Set<TransportationRoute>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Market> Markets => Set<Market>();

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<BusinessProcess> BusinessProcesses => Set<BusinessProcess>();
    public DbSet<EmployeeGroup> EmployeeGroups => Set<EmployeeGroup>();
    public DbSet<EnterpriseApplication> EnterpriseApplications => Set<EnterpriseApplication>();
    public DbSet<InfrastructureNode> InfrastructureNodes => Set<InfrastructureNode>();

    public DbSet<InventorySnapshot> InventorySnapshots => Set<InventorySnapshot>();
    public DbSet<DemandSnapshot> DemandSnapshots => Set<DemandSnapshot>();
    public DbSet<CapacitySnapshot> CapacitySnapshots => Set<CapacitySnapshot>();

    public DbSet<EnterpriseStateSnapshot> EnterpriseStateSnapshots => Set<EnterpriseStateSnapshot>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<ScenarioParameter> ScenarioParameters => Set<ScenarioParameter>();

    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();
    public DbSet<SimulationResult> SimulationResults => Set<SimulationResult>();

    public DbSet<OptimizationRun> OptimizationRuns => Set<OptimizationRun>();
    public DbSet<OptimizationConstraint> OptimizationConstraints => Set<OptimizationConstraint>();
    public DbSet<OptimizationResult> OptimizationResults => Set<OptimizationResult>();
    public DbSet<Mitigation> Mitigations => Set<Mitigation>();

    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<DecisionOption> DecisionOptions => Set<DecisionOption>();
    public DbSet<DecisionApproval> DecisionApprovals => Set<DecisionApproval>();
    public DbSet<DecisionExplanation> DecisionExplanations => Set<DecisionExplanation>();

    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Forecast> Forecasts => Set<Forecast>();

    public DbSet<Execution> Executions => Set<Execution>();
    public DbSet<ExecutionResult> ExecutionResults => Set<ExecutionResult>();

    public DbSet<DecisionOutcome> DecisionOutcomes => Set<DecisionOutcome>();
    public DbSet<DecisionQuality> DecisionQualities => Set<DecisionQuality>();

    public DbSet<ResilienceScore> ResilienceScores => Set<ResilienceScore>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(4);
                }
            }
        }

        builder.Entity<EnterpriseOrganization>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<Asset>(b =>
        {
            b.HasIndex(x => x.AssetCode).IsUnique();
            b.HasIndex(x => new { x.AssetType, x.Region });
            b.HasIndex(x => x.RiskScore);
        });

        builder.Entity<Supplier>(b =>
        {
            b.HasIndex(x => x.SupplierCode).IsUnique();
            b.HasIndex(x => x.Region);
        });

        builder.Entity<Factory>(b =>
        {
            b.HasIndex(x => x.FactoryCode).IsUnique();
            b.HasIndex(x => x.Region);
        });

        builder.Entity<Warehouse>(b =>
        {
            b.HasIndex(x => x.WarehouseCode).IsUnique();
            b.HasIndex(x => x.Region);
        });

        builder.Entity<DistributionCenter>(b =>
        {
            b.HasIndex(x => x.DcCode).IsUnique();
        });

        builder.Entity<TransportationRoute>(b =>
        {
            b.HasIndex(x => x.RouteCode).IsUnique();
            b.HasIndex(x => new { x.OriginAssetCode, x.DestinationAssetCode });
        });

        builder.Entity<Product>(b =>
        {
            b.HasIndex(x => x.Sku).IsUnique();
            b.HasIndex(x => x.Category);
        });

        builder.Entity<Customer>(b =>
        {
            b.HasIndex(x => x.CustomerCode).IsUnique();
            b.HasIndex(x => x.MarketCode);
        });

        builder.Entity<Market>(b =>
        {
            b.HasIndex(x => x.MarketCode).IsUnique();
        });

        builder.Entity<Dependency>(b =>
        {
            b.HasIndex(x => x.SourceAssetId);
            b.HasIndex(x => x.TargetAssetId);
            b.HasIndex(x => new { x.SourceAssetCode, x.TargetAssetCode });
        });

        builder.Entity<Scenario>(b =>
        {
            b.HasIndex(x => x.ScenarioCode).IsUnique();
            b.HasMany(x => x.Parameters)
                .WithOne()
                .HasForeignKey(p => p.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SimulationRun>(b =>
        {
            b.HasIndex(x => x.RunCode).IsUnique();
            b.HasIndex(x => x.ScenarioId);
            b.HasMany(x => x.Results)
                .WithOne()
                .HasForeignKey(r => r.SimulationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OptimizationRun>(b =>
        {
            b.HasIndex(x => x.RunCode).IsUnique();
            b.HasMany(x => x.Constraints)
                .WithOne()
                .HasForeignKey(c => c.OptimizationRunId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Results)
                .WithOne()
                .HasForeignKey(r => r.OptimizationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Decision>(b =>
        {
            b.HasIndex(x => x.DecisionCode).IsUnique();
            b.HasMany(x => x.Options)
                .WithOne()
                .HasForeignKey(o => o.DecisionId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Approvals)
                .WithOne()
                .HasForeignKey(a => a.DecisionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Execution>(b =>
        {
            b.HasIndex(x => x.ExecutionCode).IsUnique();
            b.HasMany(x => x.Results)
                .WithOne()
                .HasForeignKey(r => r.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(b =>
        {
            b.HasIndex(x => x.OccurredAtUtc);
            b.HasIndex(x => new { x.ModuleName, x.ActionType });
        });
    }
}
