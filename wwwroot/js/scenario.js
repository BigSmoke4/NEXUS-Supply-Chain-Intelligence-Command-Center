(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    const presetSelect = document.getElementById('scenario-preset-selector');
    if (!presetSelect) return;

    presetSelect.addEventListener('change', function () {
      const val = presetSelect.value;
      const nameInput = document.getElementById('scn-name');
      const typeInput = document.getElementById('scn-type');
      const assetInput = document.getElementById('scn-asset');
      const durInput = document.getElementById('scn-duration');
      const supCapInput = document.getElementById('scn-sup-cap');
      const demInput = document.getElementById('scn-demand');
      const trnInput = document.getElementById('scn-transport');

      if (val === 'hero' && nameInput) {
        nameInput.value = 'Hero Scenario: European Supplier 40% Capacity Loss (14 Days)';
        typeInput.value = 'Supplier Failure';
        assetInput.value = 'SUP-001';
        durInput.value = '14';
        supCapInput.value = '60';
        demInput.value = '0';
        trnInput.value = '10';
      } else if (val === 'compound' && nameInput) {
        nameInput.value = 'Supplier Capacity 50% + Demand +30% + Transport +20%';
        typeInput.value = 'Demand Spike';
        assetInput.value = 'SUP-001';
        durInput.value = '21';
        supCapInput.value = '50';
        demInput.value = '30';
        trnInput.value = '20';
      }
    });
  });
})();
