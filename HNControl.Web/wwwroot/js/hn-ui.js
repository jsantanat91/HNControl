// HN Control - UI helpers
(function () {
  const KEY = 'hn.sidebar.collapsed';

  function applySidebarState(collapsed) {
    document.body.classList.toggle('hn-sidebar-collapsed', collapsed);
    document.querySelectorAll('[data-hn-sidebar-toggle]').forEach(btn => {
      btn.setAttribute('aria-pressed', collapsed ? 'true' : 'false');
      btn.setAttribute('title', collapsed ? 'Expandir menu' : 'Compactar menu');
    });
  }

  function bootSidebarToggle() {
    const toggles = document.querySelectorAll('[data-hn-sidebar-toggle]');
    if (!toggles.length) return;

    const stored = localStorage.getItem(KEY);
    applySidebarState(stored === '1');

    toggles.forEach(btn => {
      btn.addEventListener('click', () => {
        const next = !document.body.classList.contains('hn-sidebar-collapsed');
        applySidebarState(next);
        localStorage.setItem(KEY, next ? '1' : '0');
      });
    });

    // Tooltips nativos para modo iconos
    document.querySelectorAll('.hn-sidebar .hn-side-link').forEach(link => {
      if (!link.getAttribute('title')) {
        const label = link.querySelector('.hn-side-text')?.textContent?.trim();
        if (label) link.setAttribute('title', label);
      }
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bootSidebarToggle);
  } else {
    bootSidebarToggle();
  }
})();
