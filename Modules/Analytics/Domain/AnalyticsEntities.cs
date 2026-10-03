using NEXUS.Shared.Kernel;

namespace NEXUS.Modules.Analytics.Domain;

public sealed class ResilienceScore : EntityBase
{
    public decimal OverallScore { get; set; } = 87.0m;

    public decimal SupplierDiversityScore { get; set; } = 84.0m;

    public decimal CapacityRedundancyScore { get; set; } = 88.0m;

    public decimal InventoryBufferScore { get; set; } = 85.0m;

    public decimal DependencyRiskScore { get; set; } = 81.0m;

    public decimal TransportationRedundancyScore { get; set; } = 89.0m;

    public decimal RecoveryCapabilityScore { get; set; } = 86.0m;

    public decimal ServiceLevelScore { get; set; } = 96.0m;

    public decimal OperationalFlexibilityScore { get; set; } = 87.0m;

    public decimal SimulatedCurrentDisruptedScore { get; set; } = 82.0m;

    public decimal SimulatedAfterMitigationScore { get; set; } = 94.0m;

    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}
