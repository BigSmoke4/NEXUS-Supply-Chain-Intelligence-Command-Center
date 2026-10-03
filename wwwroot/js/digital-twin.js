(function () {
  'use strict';

  window.NEXUS = window.NEXUS || {};

  window.NEXUS.onGraphNodeSelected = async function (node) {
    const titleEl = document.getElementById('inspector-asset-name');
    const codeEl = document.getElementById('inspector-asset-code');
    const utilEl = document.getElementById('inspector-utilization');
    const capEl = document.getElementById('inspector-capacity');
    const loadEl = document.getElementById('inspector-load');
    const riskEl = document.getElementById('inspector-risk');
    const whEl = document.getElementById('inspector-warehouses');
    const expEl = document.getElementById('inspector-exposure');
    const hiddenInput = document.getElementById('input-override-asset-code');
    const availSlider = document.getElementById('input-override-availability');
    const impactLink = document.getElementById('link-analyze-impact');

    if (titleEl) titleEl.textContent = node.name;
    if (codeEl) codeEl.textContent = node.assetCode + ' (' + node.assetType + ')';
    if (utilEl) utilEl.textContent = Number(node.utilizationPct).toFixed(0) + '%';
    if (capEl) capEl.textContent = Number(node.capacity).toLocaleString() + ' units/day';
    if (loadEl) loadEl.textContent = Number(node.currentLoad).toLocaleString() + ' units/day';
    if (riskEl) riskEl.textContent = node.riskLevel;
    if (whEl) whEl.textContent = String(node.dependentWarehousesCount || 0);
    if (expEl) expEl.textContent = window.NEXUS.formatCurrency(node.dailyRevenueExposureUsd) + '/day';
    if (hiddenInput) hiddenInput.value = node.assetCode;
    if (availSlider) availSlider.value = Math.round(node.availabilityPct || 100);
    if (impactLink) impactLink.href = '/DependencyGraph?asset=' + encodeURIComponent(node.assetCode);
  };
})();
