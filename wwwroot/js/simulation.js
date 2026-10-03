(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    const formSim = document.getElementById('form-run-simulation');
    const btnSim = document.getElementById('btn-submit-simulation');
    if (formSim && btnSim) {
      formSim.addEventListener('submit', function () {
        btnSim.disabled = true;
        btnSim.textContent = 'RUNNING MONTE CARLO FUTURES...';
      });
    }
  });
})();
