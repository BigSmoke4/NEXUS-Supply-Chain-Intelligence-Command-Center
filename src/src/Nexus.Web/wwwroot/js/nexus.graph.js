// nexus.graph.js — SVG dependency-graph renderer with pan/zoom, search,
// focus mode, a minimap, and keyboard accessibility (§25). Zero-dependency
// by design so it works with no npm/CDN setup at this scaffold stage. §29
// calls for a dedicated graph library (Cytoscape.js/D3) once node counts
// reach production scale for true force-directed clustering - render() is
// the single swap point; every caller only depends on the public functions
// returned below, not on SVG internals.
Nexus.graph = (() => {
    const WORLD_WIDTH = 900, WORLD_HEIGHT = 420;
    let panX = 0, panY = 0, zoom = 1;
    let isDragging = false, dragStartX = 0, dragStartY = 0;
    let currentSvg = null, currentWorld = null, currentContainer = null;

    function layoutRadial(nodes) {
        const groups = {};
        nodes.forEach(n => { (groups[n.type] = groups[n.type] || []).push(n); });
        const typeOrder = ['Supplier', 'Component', 'Factory', 'Warehouse', 'Product', 'Customer'];
        const colWidth = WORLD_WIDTH / typeOrder.length;

        const positioned = {};
        typeOrder.forEach((type, colIdx) => {
            const items = groups[type] || [];
            items.forEach((n, i) => {
                const x = colWidth * colIdx + colWidth / 2;
                const y = (WORLD_HEIGHT / (items.length + 1)) * (i + 1);
                positioned[n.id] = { ...n, x, y };
            });
        });
        return positioned;
    }

    // Dependency-free force-directed layout for larger networks (§25/§29).
    // It is deterministic (stable initial positions from node ids), bounded,
    // and uses the actual graph edges for attraction. The renderer remains
    // zero-dependency, so an eventual D3/Cytoscape adapter can replace only
    // this layout function without changing focus, minimap, keyboard or
    // cascade behavior.
    function layoutForce(nodes, edges) {
        const count = nodes.length;
        const positions = {};
        const velocity = {};
        const nodeIndex = new Map(nodes.map((n, i) => [n.id, i]));
        const adjacency = Array.from({ length: count }, () => []);

        function hash(id) {
            let h = 2166136261;
            for (let i = 0; i < id.length; i++) h = Math.imul(h ^ id.charCodeAt(i), 16777619);
            return (h >>> 0) / 4294967295;
        }

        nodes.forEach((n, i) => {
            const angle = hash(n.id) * Math.PI * 2;
            const radius = 70 + hash(n.id + ':r') * 140;
            positions[n.id] = {
                ...n,
                x: WORLD_WIDTH / 2 + Math.cos(angle) * radius,
                y: WORLD_HEIGHT / 2 + Math.sin(angle) * radius
            };
            velocity[i] = { x: 0, y: 0 };
        });

        edges.forEach(e => {
            const a = nodeIndex.get(e.source), b = nodeIndex.get(e.target);
            if (a === undefined || b === undefined || a === b) return;
            adjacency[a].push(b);
            adjacency[b].push(a);
        });

        const iterations = Math.min(110, Math.max(55, Math.round(7500 / Math.max(1, count))));
        const area = WORLD_WIDTH * WORLD_HEIGHT;
        const ideal = Math.sqrt(area / Math.max(1, count)) * 0.85;
        const repulsion = ideal * ideal;

        for (let iteration = 0; iteration < iterations; iteration++) {
            const forces = Array.from({ length: count }, () => ({ x: 0, y: 0 }));
            const cooling = 1 - iteration / iterations;

            for (let i = 0; i < count; i++) {
                for (let j = i + 1; j < count; j++) {
                    const a = positions[nodes[i].id], b = positions[nodes[j].id];
                    let dx = a.x - b.x, dy = a.y - b.y;
                    const distanceSq = Math.max(25, dx * dx + dy * dy);
                    const distance = Math.sqrt(distanceSq);
                    const force = Math.min(180, repulsion / distanceSq);
                    dx /= distance; dy /= distance;
                    forces[i].x += dx * force;
                    forces[i].y += dy * force;
                    forces[j].x -= dx * force;
                    forces[j].y -= dy * force;
                }
            }

            edges.forEach(e => {
                const i = nodeIndex.get(e.source), j = nodeIndex.get(e.target);
                if (i === undefined || j === undefined) return;
                const a = positions[e.source], b = positions[e.target];
                let dx = b.x - a.x, dy = b.y - a.y;
                const distance = Math.max(1, Math.sqrt(dx * dx + dy * dy));
                const force = (distance - ideal) * 0.012;
                dx /= distance; dy /= distance;
                forces[i].x += dx * force;
                forces[i].y += dy * force;
                forces[j].x -= dx * force;
                forces[j].y -= dy * force;
            });

            // Weak type-band gravity preserves supply-chain readability while
            // still allowing cross-type relationships to pull nodes together.
            const typeIndex = { Supplier: 0, Component: 1, Factory: 2, Warehouse: 3, Product: 4, Customer: 5 };
            nodes.forEach((n, i) => {
                const targetX = 70 + ((typeIndex[n.type] ?? 2) / 5) * (WORLD_WIDTH - 140);
                forces[i].x += (targetX - positions[n.id].x) * 0.0015;
            });

            for (let i = 0; i < count; i++) {
                velocity[i].x = (velocity[i].x + forces[i].x) * 0.72;
                velocity[i].y = (velocity[i].y + forces[i].y) * 0.72;
                const maxStep = 8 * Math.max(0.25, cooling);
                velocity[i].x = Math.max(-maxStep, Math.min(maxStep, velocity[i].x));
                velocity[i].y = Math.max(-maxStep, Math.min(maxStep, velocity[i].y));
                positions[nodes[i].id].x = Math.max(25, Math.min(WORLD_WIDTH - 25, positions[nodes[i].id].x + velocity[i].x));
                positions[nodes[i].id].y = Math.max(25, Math.min(WORLD_HEIGHT - 25, positions[nodes[i].id].y + velocity[i].y));
            }
        }

        return positions;
    }

    function layout(nodes, edges) {
        return nodes.length > 60 ? layoutForce(nodes, edges) : layoutRadial(nodes);
    }

    function render(container, nodes, edges) {
        panX = 0; panY = 0; zoom = 1;
        const positioned = layout(nodes, edges);

        const wrapper = document.createElement('div');
        wrapper.className = 'nx-graph-wrapper' + (nodes.length > 60 ? ' nx-graph-clustered' : '');

        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', `0 0 ${WORLD_WIDTH} ${WORLD_HEIGHT}`);
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '100%');
        svg.setAttribute('role', 'group');
        svg.setAttribute('aria-label', 'Supply chain dependency graph. Tab between nodes, press Enter to focus a node\'s neighbors.');

        const world = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        world.setAttribute('class', 'nx-world');
        svg.appendChild(world);

        edges.forEach(e => {
            const s = positioned[e.source], t = positioned[e.target];
            if (!s || !t) return;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            line.setAttribute('class', 'nx-edge');
            line.setAttribute('d', `M${s.x},${s.y} C${(s.x + t.x) / 2},${s.y} ${(s.x + t.x) / 2},${t.y} ${t.x},${t.y}`);
            line.setAttribute('data-source', e.source);
            line.setAttribute('data-target', e.target);
            world.appendChild(line);
        });

        Object.values(positioned).forEach(n => {
            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.setAttribute('class', `nx-node nx-node-${n.type.toLowerCase()}`);
            g.setAttribute('data-node-id', n.id);
            g.setAttribute('data-node-name', n.name.toLowerCase());
            g.setAttribute('transform', `translate(${n.x},${n.y})`);

            // Keyboard accessibility (§63/§25): every node is a focusable,
            // labelled, actionable element - not just a mouse target. A
            // screen reader announces "Supplier, Supplier Alpha, button";
            // Enter/Space triggers the same focus-mode a click does.
            g.setAttribute('tabindex', '0');
            g.setAttribute('role', 'button');
            g.setAttribute('aria-label', `${n.type}: ${n.name}`);

            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('r', 9);
            circle.setAttribute('stroke-width', 2);
            g.appendChild(circle);

            const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('y', 22);
            text.setAttribute('text-anchor', 'middle');
            text.textContent = n.name.length > 16 ? n.name.slice(0, 15) + '…' : n.name;
            g.appendChild(text);

            // Focus mode (§25): activating a node (click or keyboard)
            // highlights it and its direct neighbors, dimming everything
            // else, reusing the edge list already rendered.
            g.addEventListener('click', () => focusNode(container, n.id));
            g.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    focusNode(container, n.id);
                }
            });

            world.appendChild(g);
        });

        wrapper.appendChild(svg);
        container.innerHTML = '';
        container.appendChild(wrapper);

        currentSvg = svg; currentWorld = world; currentContainer = container;
        wirePanZoom(svg, world);
        renderMinimap(container, positioned, edges);
    }

    function wirePanZoom(svg, world) {
        const applyTransform = () => {
            world.setAttribute('transform', `translate(${panX},${panY}) scale(${zoom})`);
            updateMinimapViewport();
        };

        svg.addEventListener('wheel', (e) => {
            e.preventDefault();
            const delta = e.deltaY > 0 ? 0.9 : 1.1;
            zoom = Math.min(4, Math.max(0.4, zoom * delta));
            applyTransform();
        }, { passive: false });

        svg.addEventListener('mousedown', (e) => {
            isDragging = true;
            dragStartX = e.clientX - panX;
            dragStartY = e.clientY - panY;
        });
        window.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            panX = e.clientX - dragStartX;
            panY = e.clientY - dragStartY;
            applyTransform();
        });
        window.addEventListener('mouseup', () => { isDragging = false; });

        applyTransform();
    }

    // Real minimap (§25): a small second SVG showing every node as a dot in
    // world-space, with a rectangle showing the main view's current
    // pan/zoom window. Dragging the rectangle (or clicking anywhere on the
    // minimap) re-centers the main graph - this is an actual navigation
    // aid tied to live pan/zoom state, not a static decorative thumbnail.
    function renderMinimap(container, positioned, edges) {
        const existing = container.querySelector('.nx-minimap');
        if (existing) existing.remove();

        const mini = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        mini.setAttribute('class', 'nx-minimap');
        mini.setAttribute('viewBox', `0 0 ${WORLD_WIDTH} ${WORLD_HEIGHT}`);
        mini.setAttribute('aria-hidden', 'true'); // decorative/navigational only; the accessible graph is the main SVG + text fallback list

        edges.forEach(e => {
            const s = positioned[e.source], t = positioned[e.target];
            if (!s || !t) return;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', s.x); line.setAttribute('y1', s.y);
            line.setAttribute('x2', t.x); line.setAttribute('y2', t.y);
            line.setAttribute('class', 'nx-minimap-edge');
            mini.appendChild(line);
        });

        Object.values(positioned).forEach(n => {
            const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            dot.setAttribute('cx', n.x); dot.setAttribute('cy', n.y);
            dot.setAttribute('r', 3);
            dot.setAttribute('class', 'nx-minimap-dot');
            mini.appendChild(dot);
        });

        const viewport = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        viewport.setAttribute('class', 'nx-minimap-viewport');
        mini.appendChild(viewport);

        mini.addEventListener('mousedown', (e) => recenterFromMinimap(mini, e));
        container.appendChild(mini);
        updateMinimapViewport();
    }

    function updateMinimapViewport() {
        if (!currentContainer) return;
        const viewport = currentContainer.querySelector('.nx-minimap-viewport');
        if (!viewport) return;

        // The visible world-space window is the inverse of the world
        // transform: at zoom Z with pan (panX,panY), the visible rectangle
        // in world coordinates spans (-panX/Z .. (-panX+900)/Z) etc.
        const w = WORLD_WIDTH / zoom;
        const h = WORLD_HEIGHT / zoom;
        const x = -panX / zoom;
        const y = -panY / zoom;
        viewport.setAttribute('x', x);
        viewport.setAttribute('y', y);
        viewport.setAttribute('width', w);
        viewport.setAttribute('height', h);
    }

    function recenterFromMinimap(mini, event) {
        const rect = mini.getBoundingClientRect();
        const clickX = ((event.clientX - rect.left) / rect.width) * WORLD_WIDTH;
        const clickY = ((event.clientY - rect.top) / rect.height) * WORLD_HEIGHT;

        // Re-center the main view on the clicked world coordinate.
        panX = WORLD_WIDTH / 2 - clickX * zoom;
        panY = WORLD_HEIGHT / 2 - clickY * zoom;
        if (currentWorld) currentWorld.setAttribute('transform', `translate(${panX},${panY}) scale(${zoom})`);
        updateMinimapViewport();
    }

    function focusNode(container, nodeId) {
        const allNodes = container.querySelectorAll('.nx-node');
        const allEdges = container.querySelectorAll('.nx-edge');
        const neighborIds = new Set([nodeId]);

        allEdges.forEach(edge => {
            const source = edge.getAttribute('data-source');
            const target = edge.getAttribute('data-target');
            if (source === nodeId) neighborIds.add(target);
            if (target === nodeId) neighborIds.add(source);
        });

        allNodes.forEach(n => {
            const isRelevant = neighborIds.has(n.getAttribute('data-node-id'));
            n.style.opacity = isRelevant ? '1' : '0.15';
        });
        allEdges.forEach(edge => {
            const source = edge.getAttribute('data-source');
            const target = edge.getAttribute('data-target');
            const isRelevant = source === nodeId || target === nodeId;
            edge.style.opacity = isRelevant ? '1' : '0.08';
        });
    }

    /// Clears focus-mode dimming, restoring every node/edge to full opacity.
    function clearFocus(container) {
        container.querySelectorAll('.nx-node, .nx-edge').forEach(el => { el.style.opacity = ''; });
    }

    /// Search/filter (§25): dims every node whose name doesn't match the
    /// query, without re-rendering the graph or losing pan/zoom state.
    function searchNodes(container, query) {
        const q = query.trim().toLowerCase();
        const allNodes = container.querySelectorAll('.nx-node');
        if (!q) { allNodes.forEach(n => { n.style.opacity = ''; }); return; }

        allNodes.forEach(n => {
            const matches = n.getAttribute('data-node-name').includes(q);
            n.style.opacity = matches ? '1' : '0.12';
        });
    }

    function init() {
        const el = document.getElementById('nexus-graph-canvas');
        if (!el) return;
        try {
            const nodes = JSON.parse(el.dataset.nodes || '[]');
            const edges = JSON.parse(el.dataset.edges || '[]');
            render(el, nodes, edges);
        } catch (err) {
            console.error('NEXUS graph render failed', err);
        }
    }

    // Cascading-failure animation (§56): highlights nodes stage by stage
    // using the actual BFS-depth propagation path computed by
    // SimulationEngine (CascadeStep.StageOrder) - not a scripted animation,
    // a replay of the real graph traversal the simulation performed.
    function animateCascade(container, cascadeSteps, stageDelayMs = 700) {
        const stages = {};
        cascadeSteps.forEach(s => { (stages[s.stageOrder] = stages[s.stageOrder] || []).push(s); });
        const stageNumbers = Object.keys(stages).map(Number).sort((a, b) => a - b);

        stageNumbers.forEach((stageNum, i) => {
            setTimeout(() => {
                stages[stageNum].forEach(step => {
                    const el = container.querySelector(`[data-node-id="${step.nodeId}"] circle`);
                    if (el) {
                        el.classList.add('nx-cascade-hit');
                        el.style.fill = 'var(--nexus-danger)';
                        el.style.stroke = 'var(--nexus-danger)';
                    }
                });
            }, i * stageDelayMs);
        });
    }

    return { init, render, animateCascade, focusNode, clearFocus, searchNodes };
})();

document.addEventListener('DOMContentLoaded', Nexus.graph.init);
