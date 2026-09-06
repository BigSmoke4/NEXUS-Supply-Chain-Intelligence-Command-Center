// nexus.core.js — small shared utilities. No globals beyond the Nexus namespace.
window.Nexus = window.Nexus || {};

Nexus.core = {
    toast(message) {
        const el = document.createElement('div');
        el.className = 'nexus-toast';
        el.textContent = message;
        document.body.appendChild(el);
        setTimeout(() => el.remove(), 4000);
    },
    qs(selector, root = document) { return root.querySelector(selector); },
    qsa(selector, root = document) { return Array.from(root.querySelectorAll(selector)); }
};
