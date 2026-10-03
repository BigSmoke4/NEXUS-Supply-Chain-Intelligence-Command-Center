using System.ComponentModel.DataAnnotations;
using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.AssetManagement.Domain;

public enum EnterpriseAssetType
{
    Supplier,
    Factory,
    Warehouse,
    DistributionCenter,
    TransportationRoute,
    Product,
    Customer,
    Market,
    EmployeeGroup,
    BusinessProcess,
    Application,
    Infrastructure
}

public sealed class Asset : EntityBase
{
    [Required, MaxLength(64)]
    public string AssetCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string AssetType { get; set; } = nameof(EnterpriseAssetType.Supplier);

    [MaxLength(80)]
    public string Region { get; set; } = "EU-Central";

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public decimal Capacity { get; set; }

    public decimal CurrentLoad { get; set; }

    public decimal UtilizationPct { get; set; }

    public decimal UnitCostUsd { get; set; }

    public decimal ReliabilityPct { get; set; }

    public int LeadTimeDays { get; set; }

    public decimal RiskScore { get; set; }

    [MaxLength(32)]
    public string RiskLevel { get; set; } = "LOW";

    public decimal AvailabilityPct { get; set; } = 100m;

    public decimal DailyRevenueExposureUsd { get; set; }

    public int DependentWarehousesCount { get; set; }

    public int DownstreamAssetsCount { get; set; }

    [MaxLength(32)]
    public string OperationalStatus { get; set; } = "ONLINE";

    public bool IsBottleneck { get; set; }

    public bool IsCriticalPathNode { get; set; }
}

public sealed class BusinessProcess : EntityBase
{
    [Required, MaxLength(64)]
    public string ProcessCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = "Manufacturing & Fulfillment";

    [MaxLength(80)]
    public string OwnerDepartment { get; set; } = "Global Operations";

    public decimal CriticalityScore { get; set; } = 85m;

    public int TargetSlaHours { get; set; } = 24;

    public decimal HourlyFailureCostUsd { get; set; } = 45_000m;

    public decimal AutomationPct { get; set; } = 75m;

    public decimal AvailabilityPct { get; set; } = 99.2m;
}

public sealed class EmployeeGroup : EntityBase
{
    [Required, MaxLength(64)]
    public string GroupCode { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string Department { get; set; } = string.Empty;

    [MaxLength(120)]
    public string RoleSpecialization { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AssignedFacilityCode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Location { get; set; } = string.Empty;

    public int Headcount { get; set; }

    public decimal ActiveShiftUtilizationPct { get; set; }

    public decimal AvailabilityPct { get; set; } = 96m;

    public decimal DailyLaborCostUsd { get; set; }
}

public sealed class EnterpriseApplication : EntityBase
{
    [Required, MaxLength(64)]
    public string AppCode { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Tier { get; set; } = "Mission-Critical Tier 1";

    public decimal AvailabilityPct { get; set; } = 99.95m;

    public decimal RtoHours { get; set; } = 1.0m;

    public int DependentProcessesCount { get; set; } = 12;

    public decimal DailyOperatingCostUsd { get; set; } = 12_500m;
}

public sealed class InfrastructureNode : EntityBase
{
    [Required, MaxLength(64)]
    public string InfraCode { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string NodeType { get; set; } = "Cloud / Industrial IoT Telemetry Cluster";

    [MaxLength(80)]
    public string Region { get; set; } = "EU-Central";

    public decimal CapacityUtilizationPct { get; set; } = 68m;

    [MaxLength(32)]
    public string RedundancyLevel { get; set; } = "N+2 Active-Active";

    public decimal HealthScorePct { get; set; } = 98.4m;
}
