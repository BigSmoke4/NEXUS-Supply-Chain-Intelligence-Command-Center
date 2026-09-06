using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Alerts;
using Xunit;

namespace Nexus.Tests;

public class AlertServiceTests
{
    [Fact]
    public async Task GenerateFromScenarioResultAsync_HighRevenueAtRisk_CreatesCriticalAlert()
    {
        var (db, orgId) = TestDb.Create();
        var service = new AlertService(db, new Nexus.Web.Modules.Observability.NexusMetrics());
        var scenarioId = Guid.NewGuid();

        var result = new ScenarioResult { ScenarioId = scenarioId, RevenueAtRisk = 2_000_000m, ServiceLevelPercent = 95 };

        await service.GenerateFromScenarioResultAsync(orgId, scenarioId, result);

        var alert = Assert.Single(db.Alerts, a => a.Title == "High revenue exposure");
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(AlertState.Open, alert.State);
    }

    [Fact]
    public async Task GenerateFromScenarioResultAsync_LowRevenueAtRisk_CreatesNoRevenueAlert()
    {
        var (db, orgId) = TestDb.Create();
        var service = new AlertService(db, new Nexus.Web.Modules.Observability.NexusMetrics());
        var scenarioId = Guid.NewGuid();

        var result = new ScenarioResult { ScenarioId = scenarioId, RevenueAtRisk = 1000m, ServiceLevelPercent = 95 };

        await service.GenerateFromScenarioResultAsync(orgId, scenarioId, result);

        Assert.DoesNotContain(db.Alerts, a => a.Title.Contains("revenue exposure"));
    }

    [Fact]
    public async Task GenerateFromScenarioResultAsync_ImminentStockout_CreatesCriticalAlert()
    {
        var (db, orgId) = TestDb.Create();
        var service = new AlertService(db, new Nexus.Web.Modules.Observability.NexusMetrics());
        var scenarioId = Guid.NewGuid();

        var result = new ScenarioResult { ScenarioId = scenarioId, RevenueAtRisk = 0, ServiceLevelPercent = 95 };
        result.StockoutEvents.Add(new StockoutEvent { ComponentId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), StockoutDayOffset = 5 });

        await service.GenerateFromScenarioResultAsync(orgId, scenarioId, result);

        Assert.Contains(db.Alerts, a => a.Title == "Imminent stockout" && a.Severity == AlertSeverity.Critical);
    }

    [Fact]
    public async Task AcknowledgeAsync_SetsStateAndTimestamp()
    {
        var (db, orgId) = TestDb.Create();
        var service = new AlertService(db, new Nexus.Web.Modules.Observability.NexusMetrics());
        var scenarioId = Guid.NewGuid();
        await service.GenerateFromScenarioResultAsync(orgId, scenarioId,
            new ScenarioResult { ScenarioId = scenarioId, RevenueAtRisk = 2_000_000m, ServiceLevelPercent = 95 });

        var alert = db.Alerts.First();
        await service.AcknowledgeAsync(alert.Id, "test-user");

        var updated = db.Alerts.First(a => a.Id == alert.Id);
        Assert.Equal(AlertState.Acknowledged, updated.State);
        Assert.Equal("test-user", updated.AcknowledgedBy);
        Assert.NotNull(updated.AcknowledgedAtUtc);
    }
}
