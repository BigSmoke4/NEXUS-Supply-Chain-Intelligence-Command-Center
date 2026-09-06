using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Observability;

namespace Nexus.Web.Modules.Alerts;

public interface IAlertService
{
    /// Deterministic rule-based alert generation (§36) from a completed
    /// simulation - thresholds are fixed constants here; a production build
    /// would source them from configurable AlertOptions like the risk
    /// weights in Modules/Risk, following the same "don't hard-code
    /// everywhere" principle as §7.
    Task GenerateFromScenarioResultAsync(Guid organizationId, Guid scenarioId, ScenarioResult result, CancellationToken ct = default);
    Task AcknowledgeAsync(Guid alertId, string? user, CancellationToken ct = default);
    Task ResolveAsync(Guid alertId, CancellationToken ct = default);
}

public class AlertService : IAlertService
{
    private const decimal CriticalRevenueThreshold = 1_000_000m;
    private const decimal WarningRevenueThreshold = 250_000m;
    private const int CriticalStockoutDayThreshold = 14;

    private readonly NexusDbContext _db;
    private readonly NexusMetrics _metrics;
    public AlertService(NexusDbContext db, NexusMetrics metrics) { _db = db; _metrics = metrics; }

    public async Task GenerateFromScenarioResultAsync(Guid organizationId, Guid scenarioId, ScenarioResult result, CancellationToken ct = default)
    {
        var generated = new List<Alert>();

        void Raise(AlertSeverity severity, string title, string detail)
        {
            var alert = new Alert { OrganizationId = organizationId, ScenarioId = scenarioId, Severity = severity, Title = title, Detail = detail };
            _db.Alerts.Add(alert);
            generated.Add(alert);
        }

        if (result.RevenueAtRisk >= CriticalRevenueThreshold)
        {
            Raise(AlertSeverity.Critical, "High revenue exposure",
                $"Revenue at risk (${result.RevenueAtRisk:N0}) exceeds the critical threshold of ${CriticalRevenueThreshold:N0}.");
        }
        else if (result.RevenueAtRisk >= WarningRevenueThreshold)
        {
            Raise(AlertSeverity.Warning, "Elevated revenue exposure",
                $"Revenue at risk (${result.RevenueAtRisk:N0}) exceeds the warning threshold of ${WarningRevenueThreshold:N0}.");
        }

        var earliestStockout = result.StockoutEvents.OrderBy(s => s.StockoutDayOffset).FirstOrDefault();
        if (earliestStockout is not null && earliestStockout.StockoutDayOffset <= CriticalStockoutDayThreshold)
        {
            Raise(AlertSeverity.Critical, "Imminent stockout",
                $"Projected stockout in {earliestStockout.StockoutDayOffset} days - inside the " +
                $"{CriticalStockoutDayThreshold}-day critical response window.");
        }

        if (result.ServiceLevelPercent < 80)
        {
            Raise(AlertSeverity.Warning, "Service level degradation",
                $"Projected service level ({result.ServiceLevelPercent}%) has fallen below the 80% target.");
        }

        await _db.SaveChangesAsync(ct);
        _metrics.AlertsGenerated.Add(generated.Count);
    }

    public async Task AcknowledgeAsync(Guid alertId, string? user, CancellationToken ct = default)
    {
        var alert = await _db.Alerts.FindAsync(new object[] { alertId }, ct);
        if (alert is null) return;
        alert.State = AlertState.Acknowledged;
        alert.AcknowledgedAtUtc = DateTime.UtcNow;
        alert.AcknowledgedBy = user;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResolveAsync(Guid alertId, CancellationToken ct = default)
    {
        var alert = await _db.Alerts.FindAsync(new object[] { alertId }, ct);
        if (alert is null) return;
        alert.State = AlertState.Resolved;
        alert.ResolvedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
