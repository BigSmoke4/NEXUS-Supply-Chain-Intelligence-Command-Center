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
    }
}
