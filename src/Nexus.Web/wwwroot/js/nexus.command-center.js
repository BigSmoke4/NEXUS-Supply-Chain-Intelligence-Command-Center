// nexus.command-center.js — command center page interactions: graph search
// and focus-mode controls (§25).
document.addEventListener('DOMContentLoaded', () => {
    const canvas = document.getElementById('nexus-graph-canvas');
    const searchInput = document.getElementById('graph-search');
    const clearFocusBtn = document.getElementById('graph-clear-focus');
    if (!canvas) return;

    searchInput?.addEventListener('input', (e) => Nexus.graph.searchNodes(canvas, e.target.value));
    clearFocusBtn?.addEventListener('click', () => {
        Nexus.graph.clearFocus(canvas);
        if (searchInput) searchInput.value = '';
    });
});
