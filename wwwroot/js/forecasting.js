(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    const formPredict = document.getElementById('form-run-prediction');
    const btnPredict = document.getElementById('btn-submit-prediction');
    if (formPredict && btnPredict) {
      formPredict.addEventListener('submit', function () {
        btnPredict.disabled = true;
        btnPredict.textContent = 'RUNNING ML INFERENCE PIPELINE...';
      });
    }
  });
})();
