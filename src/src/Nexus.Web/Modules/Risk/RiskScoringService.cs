using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.Risk;

/// Configurable weights per §7 - never hard-code these inline at call sites.
public class SupplierRiskWeights
{
    public double Financial { get; set; } = 0.15;
    public double Geopolitical { get; set; } = 0.20;
    public double Operational { get; set; } = 0.25;
    public double Transportation { get; set; } = 0.15;
    public double Quality { get; set; } = 0.10;
    public double Concentration { get; set; } = 0.15;

    public double Sum => Financial + Geopolitical + Operational + Transportation + Quality + Concentration;
}

public interface IRiskScoringService
{
    double CalculateSupplierRiskScore(Supplier supplier);
}

public class RiskScoringService : IRiskScoringService
{
    private readonly SupplierRiskWeights _weights;

    public RiskScoringService(SupplierRiskWeights weights) => _weights = weights;

    public double CalculateSupplierRiskScore(Supplier s)
    {
        // All component risks are expected on a 0-100 scale; the composite is
        // the weighted average, normalized in case weights don't sum to exactly 1.
        var raw =
            s.FinancialRisk * _weights.Financial +
            s.GeopoliticalRisk * _weights.Geopolitical +
            s.OperationalRisk * _weights.Operational +
            s.TransportationRisk * _weights.Transportation +
            s.QualityRisk * _weights.Quality +
            s.ConcentrationRisk * _weights.Concentration;

        var normalized = _weights.Sum > 0 ? raw / _weights.Sum : raw;
        return Math.Round(Math.Clamp(normalized, 0, 100), 1);
    }
}
