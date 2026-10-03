(function () {
  'use strict';

  window.NEXUS = window.NEXUS || {};

  window.NEXUS.formatCurrency = function (val) {
    const num = Number(val) || 0;
    if (Math.abs(num) >= 1_000_000) {
      return '$' + (num / 1_000_000).toFixed(2) + 'M';
    }
    if (Math.abs(num) >= 1_000) {
      return '$' + (num / 1_000).toFixed(0) + 'K';
    }
    return '$' + num.toFixed(0);
  };

  document.addEventListener('DOMContentLoaded', function () {
    const heroBtn = document.getElementById('btn-run-hero-demo');
    const heroStatusBox = document.getElementById('hero-demo-live-output');

    if (heroBtn) {
      heroBtn.addEventListener('click', async function () {
        heroBtn.disabled = true;
        const originalText = heroBtn.textContent;
        heroBtn.textContent = 'EXECUTING 15-STEP HERO PIPELINE...';
        if (heroStatusBox) {
          heroStatusBox.hidden = false;
          heroStatusBox.innerHTML = '<div class="digital-display">Initializing European Supplier SUP-001 40% Capacity Disruption (14 Days) -> Graph Propagation -> 10,000 Monte Carlo Futures -> Google OR-Tools Optimization -> Strategy B Approval & Execution...</div>';
        }

        try {
          const response = await fetch('/api/hero-demo/execute', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
          });
          const data = await response.json();
          if (heroStatusBox && data && data.steps) {
            const stepsHtml = data.steps.map(function (s) {
              return '<div class="replay-item">' +
                '<span class="mono-num text-amber">STEP ' + s.stepNumber + '</span>' +
                '<strong>' + s.title + '</strong>' +
                '<span>' + s.metricSummary + ' <a href="' + s.targetUrl + '">[INSPECT]</a></span>' +
                '</div>';
            }).join('');

            heroStatusBox.innerHTML =
              '<div class="command-panel" style="margin-top:12px;">' +
              '<div class="panel-header-bar"><h3 class="panel-title"><span class="status-led"></span> HERO DEMONSTRATION COMPLETED — DECISION ' + data.decisionCode + '</h3>' +
              '<span class="badge-chip optimal">RESILIENCE: ' + data.resilienceScoreBefore + ' -> ' + data.resilienceScoreAfter + '</span></div>' +
              '<div class="replay-timeline">' + stepsHtml + '</div>' +
              '</div>';
          }
        } catch (err) {
          if (heroStatusBox) {
            heroStatusBox.innerHTML = '<div class="critical-panel">Hero Demo execution error: ' + err.message + '</div>';
          }
        } finally {
          heroBtn.disabled = false;
          heroBtn.textContent = originalText;
        }
      });
    }
  });
})();
