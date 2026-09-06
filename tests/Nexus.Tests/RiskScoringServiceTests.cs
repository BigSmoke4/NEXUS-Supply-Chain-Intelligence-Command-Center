using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Risk;
using Xunit;

namespace Nexus.Tests;

public class RiskScoringServiceTests
{
    [Fact]
    public void CalculateSupplierRiskScore_WeightsSumToOne_ReturnsWeightedAverage()
    {
        var weights = new SupplierRiskWeights
        {
            Financial = 0.15, Geopolitical = 0.20, Operational = 0.25,
            Transportation = 0.15, Quality = 0.10, Concentration = 0.15
        };
        var service = new RiskScoringService(weights);

        var supplier = new Supplier
        {
            FinancialRisk = 20, GeopoliticalRisk = 80, OperationalRisk = 40,
            TransportationRisk = 30, QualityRisk = 10, ConcentrationRisk = 90
        };

        var expected = 20 * 0.15 + 80 * 0.20 + 40 * 0.25 + 30 * 0.15 + 10 * 0.10 + 90 * 0.15;

        var score = service.CalculateSupplierRiskScore(supplier);

        Assert.Equal(Math.Round(expected, 1), score);
    }

    [Fact]
    public void CalculateSupplierRiskScore_ClampsToZeroAndHundred()
    {
        var weights = new SupplierRiskWeights(); // defaults sum to 1.0
        var service = new RiskScoringService(weights);

        var allZero = new Supplier();
        var allMax = new Supplier
        {
            FinancialRisk = 100, GeopoliticalRisk = 100, OperationalRisk = 100,
            TransportationRisk = 100, QualityRisk = 100, ConcentrationRisk = 100
        };

        Assert.Equal(0, service.CalculateSupplierRiskScore(allZero));
        Assert.Equal(100, service.CalculateSupplierRiskScore(allMax));
    }

    [Fact]
    public void CalculateSupplierRiskScore_NormalizesWhenWeightsDoNotSumToOne()
    {
        // Misconfigured weights (summing to 0.5) should still produce a
        // 0-100 score via normalization, not silently understate risk.
        var weights = new SupplierRiskWeights
        {
            Financial = 0.5, Geopolitical = 0, Operational = 0,
            Transportation = 0, Quality = 0, Concentration = 0
        };
        var service = new RiskScoringService(weights);
        var supplier = new Supplier { FinancialRisk = 60 };

        var score = service.CalculateSupplierRiskScore(supplier);

        Assert.Equal(60, score);
    }
}
