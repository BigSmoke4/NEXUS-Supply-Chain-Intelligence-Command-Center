// nexus.scenario.js — scenario builder page interactions.
document.addEventListener('DOMContentLoaded', () => {
    const form = document.querySelector('#scenario-form');
    if (form) {
        form.addEventListener('submit', () => {
            const btn = form.querySelector('.nexus-button-run');
            if (btn) { btn.disabled = true; btn.textContent = 'RUNNING SIMULATION…'; }
        });
    }

    // Natural-language scenario parsing (§33): sends free text to the
    // deterministic parser and either fills the form or surfaces the exact
    // clarifications needed - it never guesses on the client either.
    const parseBtn = document.getElementById('nl-parse-btn');
    if (!parseBtn) return;

    parseBtn.addEventListener('click', async () => {
        const text = document.getElementById('nl-scenario-input').value.trim();
        const clarBox = document.getElementById('nl-clarifications');
        if (!text) return;

        parseBtn.disabled = true;
        parseBtn.textContent = 'Parsing…';
        clarBox.style.display = 'none';

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('text', text);
            if (token) formData.append('__RequestVerificationToken', token);

            const response = await Nexus.api.post(parseBtn.dataset.parseUrl, formData);
            const result = await response.json();

            if (result.succeeded) {
                document.getElementById('field-nodeId').value = result.nodeId;
                document.getElementById('field-type').value = result.type;
                document.getElementById('field-durationDays').value = result.durationDays;
                document.getElementById('field-severityPercent').value = result.severityPercent;
                Nexus.core.toast(`Parsed: ${result.nodeName}, ${result.type}, ${result.durationDays}d, ${result.severityPercent}%`);
            } else {
                clarBox.className = 'nexus-alert nexus-alert-critical';
                clarBox.style.display = 'block';
                clarBox.innerHTML = '<strong>Need more detail:</strong><br>' + result.clarifications.join('<br>');
            }
        } catch (err) {
            Nexus.core.toast('Parsing failed — see console.');
            console.error(err);
        } finally {
            parseBtn.disabled = false;
            parseBtn.textContent = 'Parse & fill form';
        }
    });
});

// Cascading-failure graph + replay button (§56) — separate listener so it
// still runs on pages (like Results) that don't have the NL-parse button
// the block above returns early without.
document.addEventListener('DOMContentLoaded', () => {
    const canvas = document.getElementById('cascade-graph-canvas');
    const animateBtn = document.getElementById('animate-cascade-btn');
    if (!canvas || !animateBtn) return;

    let nodes = [], edges = [], cascade = [];
    try {
        nodes = JSON.parse(canvas.dataset.nodes || '[]');
        edges = JSON.parse(canvas.dataset.edges || '[]');
        cascade = JSON.parse(canvas.dataset.cascade || '[]');
    } catch (err) {
        console.error('Failed to parse cascade graph data', err);
        return;
    }

    Nexus.graph.render(canvas, nodes, edges);

    animateBtn.addEventListener('click', () => {
        // Reset any highlighting from a previous replay before starting again.
        canvas.querySelectorAll('.nx-cascade-hit').forEach(el => {
            el.classList.remove('nx-cascade-hit');
            el.style.fill = '';
            el.style.stroke = '';
        });
        Nexus.graph.animateCascade(canvas, cascade);
    });
});
