using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Security;

namespace Nexus.Web.Data;

public class NexusDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentTenant _tenant;

    public NexusDbContext(DbContextOptions<NexusDbContext> options, ICurrentTenant tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<SupplyNode> SupplyNodes => Set<SupplyNode>();
    public DbSet<SupplyEdge> SupplyEdges => Set<SupplyEdge>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<Factory> Factories => Set<Factory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BillOfMaterial> BillOfMaterials => Set<BillOfMaterial>();
    public DbSet<InventoryRecord> InventoryRecords => Set<InventoryRecord>();

    public DbSet<Disruption> Disruptions => Set<Disruption>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<ScenarioResult> ScenarioResults => Set<ScenarioResult>();
    public DbSet<StockoutEvent> StockoutEvents => Set<StockoutEvent>();
    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();
    public DbSet<CascadeStep> CascadeSteps => Set<CascadeStep>();
    public DbSet<MitigationStrategy> MitigationStrategies => Set<MitigationStrategy>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SimulationJobRecord> SimulationJobRecords => Set<SimulationJobRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SupplyEdge>(e =>
        {
            e.HasOne(x => x.Source).WithMany(n => n.OutgoingEdges)
                .HasForeignKey(x => x.SourceNodeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Target).WithMany(n => n.IncomingEdges)
                .HasForeignKey(x => x.TargetNodeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.SourceNodeId });
            e.HasIndex(x => new { x.OrganizationId, x.TargetNodeId });
        });

        builder.Entity<SupplyNode>(n =>
        {
            n.HasIndex(x => new { x.OrganizationId, x.Type });
            n.HasIndex(x => new { x.OrganizationId, x.NodeRefId });
        });

        builder.Entity<InventoryRecord>(i =>
        {
            i.HasIndex(x => new { x.OrganizationId, x.ComponentId, x.WarehouseId });
        });

        builder.Entity<BillOfMaterial>().HasIndex(x => new { x.OrganizationId, x.ProductId, x.ComponentId }).IsUnique();
        builder.Entity<ScenarioResult>().HasIndex(x => new { x.OrganizationId, x.ScenarioId, x.RunAtUtc });
        builder.Entity<StockoutEvent>().HasIndex(x => new { x.OrganizationId, x.ScenarioResultId });
        builder.Entity<TimelineEvent>().HasIndex(x => new { x.OrganizationId, x.ScenarioResultId, x.DayOffset });
        builder.Entity<CascadeStep>().HasIndex(x => new { x.OrganizationId, x.ScenarioResultId, x.StageOrder });
        builder.Entity<MitigationStrategy>().HasIndex(x => new { x.OrganizationId, x.ScenarioResultId });

        builder.Entity<Scenario>()
            .HasMany(s => s.Disruptions)
            .WithOne()
            .HasForeignKey("ScenarioId")
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant isolation enforced at the data-access layer (§22): every
        // tenant-scoped entity gets a global query filter keyed off
        // ICurrentTenant, so a controller that forgets to filter by
        // OrganizationId still cannot leak another tenant's rows. When
        // _tenant.OrganizationId is null (unresolved), the comparison against
        // a non-nullable Guid column never matches, so queries fail closed
        // (return nothing) rather than open (return everything). Background
        // jobs/seeders must register a FixedCurrentTenant explicitly.
        builder.Entity<Supplier>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Component>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Factory>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Warehouse>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Product>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Customer>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<InventoryRecord>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<SupplyNode>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<SupplyEdge>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Disruption>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Scenario>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<AuditLogEntry>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<KnowledgeDocument>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<Alert>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<SimulationJobRecord>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);

        // Every tenant-owned entity is filtered here, including derived simulation
        // artifacts and Identity users. This closes the common "controller forgot
        // OrganizationId" escape hatch at the EF data-access boundary.
        builder.Entity<ApplicationUser>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<BillOfMaterial>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<ScenarioResult>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<StockoutEvent>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<TimelineEvent>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<CascadeStep>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
        builder.Entity<MitigationStrategy>().HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantWriteBoundary();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override int SaveChanges() => SaveChanges(true);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantWriteBoundary();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    private void ApplyTenantWriteBoundary()
    {
        var tenantId = _tenant.OrganizationId;
        foreach (var entry in ChangeTracker.Entries())
        {
            var property = entry.Metadata.FindProperty(nameof(Supplier.OrganizationId));
            if (property is null || entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            var current = (Guid?)entry.Property(property.Name).CurrentValue;
            if (entry.State == EntityState.Added && current == Guid.Empty && tenantId is Guid resolvedTenant)
            {
                entry.Property(property.Name).CurrentValue = resolvedTenant;
                current = resolvedTenant;
            }

            if (tenantId is Guid resolved && current is Guid ownedBy && ownedBy != Guid.Empty && ownedBy != resolved)
                throw new InvalidOperationException($"Tenant boundary violation: {entry.Metadata.ClrType.Name} belongs to organization {ownedBy}, but the current tenant is {resolved}.");

            if (tenantId is null && current is Guid unresolvedOwned && unresolvedOwned == Guid.Empty)
                throw new InvalidOperationException($"Tenant-owned entity {entry.Metadata.ClrType.Name} cannot be persisted without an organization.");
        }
    }
}
