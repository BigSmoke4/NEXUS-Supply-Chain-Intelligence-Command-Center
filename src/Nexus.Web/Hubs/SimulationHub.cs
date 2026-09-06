using Microsoft.AspNetCore.SignalR;

namespace Nexus.Web.Hubs;

/// Real-time channel (§37) for command-center updates: simulation progress,
/// agent trace steps, risk-score changes, disruption detection. Clients
/// subscribe from nexus.signalr.js and update the graph/timeline without a
/// full page refresh.
public class SimulationHub : Hub
{
    public async Task JoinScenarioGroup(string scenarioId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"scenario-{scenarioId}");
}
