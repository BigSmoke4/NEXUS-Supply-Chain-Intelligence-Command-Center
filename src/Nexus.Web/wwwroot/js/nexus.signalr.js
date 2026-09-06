// nexus.signalr.js — connects to the simulation hub for real-time updates (§37).
Nexus.signalr = (() => {
    let connection = null;

    function init() {
        if (typeof signalR === 'undefined') return; // lib not loaded in this environment
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/simulation')
            .withAutomaticReconnect()
            .build();

        connection.on('SimulationStarted', (scenarioId) => {
            Nexus.core.toast(`Simulation started for scenario ${scenarioId}`);
        });
        connection.on('SimulationCompleted', (scenarioId) => {
            Nexus.core.toast(`Simulation complete for scenario ${scenarioId}`);
            reloadIfWatchingScenario(scenarioId);
        });
        connection.on('SimulationFailed', (scenarioId) => {
            Nexus.core.toast(`Simulation failed for scenario ${scenarioId} — check server logs.`);
        });

        connection.start()
            .then(() => joinScenarioGroupIfOnResultsPage())
            .catch(err => console.error('SignalR connection failed', err));
    }

    // The scenario Results page runs its simulation in the background
    // (§46); joining the "scenario-{id}" group lets the background worker
    // target this specific page's SignalR events instead of broadcasting to
    // every connected client.
    function joinScenarioGroupIfOnResultsPage() {
        const root = document.getElementById('scenario-results-root');
        if (root && connection) {
            connection.invoke('JoinScenarioGroup', root.dataset.scenarioId).catch(err => console.error(err));
        }
    }

    function reloadIfWatchingScenario(scenarioId) {
        const root = document.getElementById('scenario-results-root');
        if (root && root.dataset.scenarioId === scenarioId) {
            window.location.reload();
        }
    }

    return { init, get connection() { return connection; } };
})();

document.addEventListener('DOMContentLoaded', Nexus.signalr.init);
