# Accessibility Engineering Pass

NEXUS is designed around WCAG 2.2 AA-friendly patterns. This document records
the implementation checks included in the repository; it is not a legal or
formal accessibility certification.

## Implemented

- Semantic `<nav>`, `<main>`, `<header>`, `<footer>`, headings, lists, tables,
  and form labels.
- Skip link to the main content region.
- Visible `:focus-visible` treatment for primary navigation and graph nodes.
- SVG graph nodes expose `tabindex`, `role="button"`, and an ARIA label.
- Enter/Space activates the same graph focus behavior as a mouse click.
- Graph relationship fallback exposes dependency direction as text.
- Search and interactive controls have explicit labels/placeholders.
- Error/alert regions use `role="alert"` where appropriate.
- Reduced-motion support disables decorative animation for users who request it.
- The neural navigation effect is decorative; the underlying control remains a
  normal keyboard-operable link list.
- The minimap is marked decorative because the main graph and text fallback are
  the accessible sources of network information.

## Verification before release

Run a browser-based accessibility scanner such as axe, Lighthouse, or WAVE,
then perform a keyboard-only pass and a screen-reader pass against the deployed
application. Check zoom/reflow at 200%, contrast in the selected theme, focus
order, error messaging, and any organization-specific content. Those checks
are environment-dependent and therefore are not represented as a false
"certified" claim in this repository.
