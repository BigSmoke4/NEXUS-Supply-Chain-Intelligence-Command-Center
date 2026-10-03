(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    const simSelect = document.getElementById('resilience-mitigation-sim-toggle');
    const simScoreEl = document.getElementById('resilience-simulated-value');
    if (simSelect && simScoreEl) {
      simSelect.addEventListener('change', function () {
        const beforeVal = simSelect.getAttribute('data-before') || '82';
        const afterVal = simSelect.getAttribute('data-after') || '94';
        simScoreEl.textContent = simSelect.checked ? afterVal + ' / 100' : beforeVal + ' / 100';
      });
    }
  });
})();
