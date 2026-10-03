(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    const btnRefreshState = document.getElementById('btn-refresh-enterprise-state');
    if (btnRefreshState) {
      btnRefreshState.addEventListener('click', async function () {
        btnRefreshState.disabled = true;
        try {
          const res = await fetch('/api/enterprise/state?refresh=true');
          if (res.ok) {
            window.location.reload();
          }
        } finally {
          btnRefreshState.disabled = false;
        }
      });
    }
  });
})();
