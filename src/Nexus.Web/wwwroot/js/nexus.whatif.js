// nexus.whatif.js — drives the What-If Mode sliders (§57), debounced so a
// fast drag doesn't flood the server with requests.
document.addEventListener('DOMContentLoaded', () => {
    const nodeSelect = document.getElementById('wi-node');
    if (!nodeSelect) return;

    const capacity = document.getElementById('wi-capacity');
    const inventory = document.getElementById('wi-inventory');
    const demand = document.getElementById('wi-demand');

    let debounceTimer = null;

    function recalculate() {
        document.getElementById('wi-capacity-val').textContent = capacity.value;
        document.getElementById('wi-inventory-val').textContent = inventory.value;
        document.getElementById('wi-demand-val').textContent = demand.value;

        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(async () => {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('nodeId', nodeSelect.value);
            formData.append('capacityPercent', capacity.value);
            formData.append('additionalInventoryDays', inventory.value);
            formData.append('demandMultiplierPercent', demand.value);
            if (token) formData.append('__RequestVerificationToken', token);

            try {
                const response = await Nexus.api.post('/WhatIf/Calculate', formData);
                const result = await response.json();

                document.getElementById('wi-revenue').textContent = `$${Number(result.revenueAtRisk).toLocaleString()}`;
                document.getElementById('wi-products').textContent = result.affectedProducts;
                document.getElementById('wi-stockout').textContent = result.stockoutDays > 0 ? `${result.stockoutDays} days` : '—';
                document.getElementById('wi-service').textContent = `${result.serviceLevel}%`;
                document.getElementById('wi-cost').textContent = `$${Number(result.additionalCost).toLocaleString()}`;
            } catch (err) {
                console.error('What-If calculation failed', err);
            }
        }, 150);
    }

    [nodeSelect, capacity, inventory, demand].forEach(el => el.addEventListener('input', recalculate));
    recalculate();
});
