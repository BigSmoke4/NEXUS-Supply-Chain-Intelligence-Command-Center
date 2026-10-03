(function () {
  'use strict';

  window.NEXUS = window.NEXUS || {};

  function initGraphCanvas(container) {
    const svg = container.querySelector('svg.graph-svg-surface');
    if (!svg) return;

    let nodes = [];
    let edges = [];
    let criticalPath = [];
    let bottlenecks = [];

    try {
      nodes = JSON.parse(container.getAttribute('data-nodes') || '[]');
      edges = JSON.parse(container.getAttribute('data-edges') || '[]');
      criticalPath = JSON.parse(container.getAttribute('data-critical-path') || '[]');
      bottlenecks = JSON.parse(container.getAttribute('data-bottlenecks') || '[]');
    } catch (e) {
      console.error('Failed to parse graph data', e);
      return;
    }

    const typeColumns = {
      'Supplier': 0,
      'Factory': 1,
      'TransportationRoute': 2,
      'Warehouse': 3,
      'DistributionCenter': 4,
      'Market': 5,
      'Customer': 6
    };

    const typeColors = {
      'Supplier': '#38bdf8',
      'Factory': '#f59e0b',
      'TransportationRoute': '#a78bfa',
      'Warehouse': '#10b981',
      'DistributionCenter': '#2dd4bf',
      'Market': '#f472b6',
      'Customer': '#fb7185'
    };

    const width = 1260;
    const height = 540;
    let scale = 1.0;
    let panX = 0;
    let panY = 0;
    let isDragging = false;
    let dragStartX = 0;
    let dragStartY = 0;

    let filterType = 'ALL';
    let searchTerm = '';
    let highlightCritical = false;
    let highlightBottlenecks = false;
    let highlightHighRisk = false;

    const buckets = [[], [], [], [], [], [], []];
    nodes.forEach(function (n) {
      const col = typeColumns[n.assetType] !== undefined ? typeColumns[n.assetType] : 3;
      buckets[col].push(n);
    });

    const nodePositions = {};
    buckets.forEach(function (colNodes, colIdx) {
      const x = 95 + colIdx * 178;
      const count = Math.max(1, colNodes.length);
      colNodes.forEach(function (n, idx) {
        const y = 48 + ((idx + 0.5) / count) * (height - 86);
        nodePositions[n.assetCode] = { x: x, y: y, node: n };
      });
    });

    function render() {
      svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
      svg.innerHTML = '';

      const gRoot = document.createElementNS('http://www.w3.org/2000/svg', 'g');
      gRoot.setAttribute('transform', 'translate(' + panX + ',' + panY + ') scale(' + scale + ')');

      // Draw stage column headers
      const colHeaders = ['SUPPLIERS', 'FACTORIES', 'TRANSPORT', 'WAREHOUSES', 'DIST. CENTERS', 'MARKETS', 'CUSTOMERS'];
      colHeaders.forEach(function (title, i) {
        const tx = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        tx.setAttribute('x', String(95 + i * 178));
        tx.setAttribute('y', '24');
        tx.setAttribute('text-anchor', 'middle');
        tx.setAttribute('fill', '#697a8f');
        tx.setAttribute('font-size', '10');
        tx.setAttribute('font-family', 'monospace');
        tx.textContent = title;
        gRoot.appendChild(tx);
      });

      const critSet = new Set(criticalPath);
      const bottleSet = new Set(bottlenecks);

      // Draw edges
      edges.forEach(function (e) {
        const src = nodePositions[e.sourceCode];
        const dst = nodePositions[e.targetCode];
        if (!src || !dst) return;

        if (filterType !== 'ALL' && src.node.assetType !== filterType && dst.node.assetType !== filterType) {
          return;
        }

        const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        line.setAttribute('x1', String(src.x));
        line.setAttribute('y1', String(src.y));
        line.setAttribute('x2', String(dst.x));
        line.setAttribute('y2', String(dst.y));

        const isCritEdge = e.isOnCriticalPath || (critSet.has(e.sourceCode) && critSet.has(e.targetCode));
        if (highlightCritical && isCritEdge) {
          line.setAttribute('stroke', '#f59e0b');
          line.setAttribute('stroke-width', '2.4');
          line.setAttribute('stroke-opacity', '0.92');
        } else if (e.isSinglePointOfFailure) {
          line.setAttribute('stroke', '#ef4444');
          line.setAttribute('stroke-width', '1.6');
          line.setAttribute('stroke-opacity', '0.55');
        } else {
          line.setAttribute('stroke', '#334155');
          line.setAttribute('stroke-width', '1');
          line.setAttribute('stroke-opacity', '0.35');
        }
        gRoot.appendChild(line);
      });

      // Draw nodes
      nodes.forEach(function (n) {
        const pos = nodePositions[n.assetCode];
        if (!pos) return;

        if (filterType !== 'ALL' && n.assetType !== filterType) return;
        if (searchTerm &&
            n.assetCode.toLowerCase().indexOf(searchTerm) === -1 &&
            n.name.toLowerCase().indexOf(searchTerm) === -1) {
          return;
        }

        const isCrit = n.isCriticalPathNode || critSet.has(n.assetCode);
        const isBottle = n.isBottleneck || bottleSet.has(n.assetCode) || n.utilizationPct >= 88;
        const isHighRisk = n.riskScore >= 60 || n.riskLevel === 'HIGH' || n.riskLevel === 'CRITICAL';

        if (highlightCritical && !isCrit) return;
        if (highlightBottlenecks && !isBottle) return;
        if (highlightHighRisk && !isHighRisk) return;

        const gNode = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        gNode.setAttribute('transform', 'translate(' + pos.x + ',' + pos.y + ')');
        gNode.style.cursor = 'pointer';

        const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        const radius = isCrit || isBottle ? 8.5 : 6.2;
        circle.setAttribute('r', String(radius));
        circle.setAttribute('fill', typeColors[n.assetType] || '#38bdf8');
        circle.setAttribute('stroke', isHighRisk ? '#ef4444' : isBottle ? '#f59e0b' : '#0f172a');
        circle.setAttribute('stroke-width', isHighRisk || isBottle ? '2.5' : '1.5');
        gNode.appendChild(circle);

        const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        label.setAttribute('x', '11');
        label.setAttribute('y', '3');
        label.setAttribute('fill', '#cbd5e1');
        label.setAttribute('font-size', '9.5');
        label.setAttribute('font-family', 'monospace');
        label.textContent = n.assetCode;
        gNode.appendChild(label);

        gNode.addEventListener('click', function () {
          if (typeof window.NEXUS.onGraphNodeSelected === 'function') {
            window.NEXUS.onGraphNodeSelected(n);
          }
        });

        gRoot.appendChild(gNode);
      });

      svg.appendChild(gRoot);
    }

    // Pan & Zoom handlers
    svg.addEventListener('mousedown', function (e) {
      isDragging = true;
      dragStartX = e.clientX - panX;
      dragStartY = e.clientY - panY;
    });
    window.addEventListener('mousemove', function (e) {
      if (!isDragging) return;
      panX = e.clientX - dragStartX;
      panY = e.clientY - dragStartY;
      render();
    });
    window.addEventListener('mouseup', function () {
      isDragging = false;
    });

    const btnZoomIn = document.getElementById('btn-graph-zoom-in');
    const btnZoomOut = document.getElementById('btn-graph-zoom-out');
    const btnReset = document.getElementById('btn-graph-reset');
    const inputSearch = document.getElementById('input-graph-search');
    const selectType = document.getElementById('select-graph-type');
    const chkCrit = document.getElementById('chk-graph-critical');
    const chkBottle = document.getElementById('chk-graph-bottlenecks');
    const chkRisk = document.getElementById('chk-graph-highrisk');

    if (btnZoomIn) btnZoomIn.addEventListener('click', function () { scale = Math.min(2.6, scale + 0.2); render(); });
    if (btnZoomOut) btnZoomOut.addEventListener('click', function () { scale = Math.max(0.5, scale - 0.2); render(); });
    if (btnReset) btnReset.addEventListener('click', function () { scale = 1.0; panX = 0; panY = 0; render(); });
    if (inputSearch) inputSearch.addEventListener('input', function () { searchTerm = inputSearch.value.trim().toLowerCase(); render(); });
    if (selectType) selectType.addEventListener('change', function () { filterType = selectType.value; render(); });
    if (chkCrit) chkCrit.addEventListener('change', function () { highlightCritical = chkCrit.checked; render(); });
    if (chkBottle) chkBottle.addEventListener('change', function () { highlightBottlenecks = chkBottle.checked; render(); });
    if (chkRisk) chkRisk.addEventListener('change', function () { highlightHighRisk = chkRisk.checked; render(); });

    render();
  }

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.js-enterprise-graph-container').forEach(initGraphCanvas);
  });
})();
