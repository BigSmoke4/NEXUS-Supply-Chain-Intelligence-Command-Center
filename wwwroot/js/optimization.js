(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    const formOpt = document.getElementById('form-run-optimization');
    const btnOpt = document.getElementById('btn-submit-optimization');
    if (formOpt && btnOpt) {
      formOpt.addEventListener('submit', function () {
        btnOpt.disabled = true;
        btnOpt.textContent = 'SOLVING GOOGLE OR-TOOLS MILP...';
      });
    }
  });
})();
