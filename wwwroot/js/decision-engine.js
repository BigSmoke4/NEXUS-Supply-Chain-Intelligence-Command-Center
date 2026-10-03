(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    // 1. Decision Time Machine Replay Animation
    const btnReplay = document.getElementById('btn-start-decision-replay');
    if (btnReplay) {
      btnReplay.addEventListener('click', function () {
        const items = Array.from(document.querySelectorAll('.replay-item'));
        items.forEach(function (el) { el.classList.remove('active-replay-step'); });
        items.forEach(function (el, idx) {
          setTimeout(function () {
            items.forEach(function (other) { other.classList.remove('active-replay-step'); });
            el.classList.add('active-replay-step');
          }, idx * 420);
        });
      });
    }

    // 2. Grounded AI Decision Assistant
    const btnAskAi = document.getElementById('btn-ask-ai-assistant');
    const inputAi = document.getElementById('input-ai-question');
    const outputAi = document.getElementById('ai-assistant-output');

    if (btnAskAi && inputAi && outputAi) {
      btnAskAi.addEventListener('click', async function () {
        btnAskAi.disabled = true;
        btnAskAi.textContent = 'ANALYZING TWIN & OPTIMIZING...';
        outputAi.hidden = false;
        outputAi.innerHTML = '<div class="digital-display">Retrieving Digital Twin telemetry -> Propagating dependencies -> Running 10,000 Monte Carlo futures -> Solving Google OR-Tools Pareto Frontier...</div>';

        try {
          const resp = await fetch('/api/decisions/assistant', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question: inputAi.value })
          });
          const data = await resp.json();
          const chainHtml = (data.groundedEvidenceChain || []).map(function (step) {
            return '<div style="margin-bottom:6px;">' + step + '</div>';
          }).join('');

          outputAi.innerHTML =
            '<div class="inset-panel" style="margin-top:10px;">' +
            '<div style="color:#fbbf24;font-family:monospace;font-weight:700;margin-bottom:8px;">GROUNDED AI DECISION RECOMMENDATION: ' + data.decisionCode + ' (' + data.recommendedStrategyCode + ')</div>' +
            '<div style="font-size:12.5px;line-height:1.55;">' + chainHtml + '</div>' +
            '<div style="margin-top:10px;"><a class="physical-button physical-button-primary" href="/DecisionEngine/Details/' + data.decisionId + '">OPEN GENERATED DECISION ' + data.decisionCode + '</a></div>' +
            '</div>';
        } catch (e) {
          outputAi.innerHTML = '<div class="critical-panel">Assistant error: ' + e.message + '</div>';
        } finally {
          btnAskAi.disabled = false;
          btnAskAi.textContent = 'ASK GROUNDED AI AGENT';
        }
      });
    }
  });
})();
