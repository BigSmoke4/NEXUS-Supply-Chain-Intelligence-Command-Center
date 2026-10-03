(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-range-display]').forEach(function (input) {
      const targetId = input.getAttribute('data-range-display');
      const targetEl = document.getElementById(targetId);
      if (targetEl) {
        input.addEventListener('input', function () {
          targetEl.textContent = input.value + (input.getAttribute('data-range-unit') || '%');
        });
      }
    });
  });
})();
