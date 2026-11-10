(() => {
    'use strict';
    const root = document.querySelector('[data-ops-key]');
    if (!root) return;
    const tabs = [...root.querySelectorAll('.hn-ops-tabs [role="tab"]')];
    const key = 'hn.operations.tab.' + root.dataset.opsKey;
    tabs.forEach((tab, index) => {
        tab.tabIndex = tab.classList.contains('active') ? 0 : -1;
        tab.addEventListener('shown.bs.tab', () => {
            tabs.forEach(item => item.tabIndex = item === tab ? 0 : -1);
            try { sessionStorage.setItem(key, tab.id); } catch { }
        });
        tab.addEventListener('keydown', event => {
            if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
            event.preventDefault();
            const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1
                : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
            tabs[next].focus();
            bootstrap.Tab.getOrCreateInstance(tabs[next]).show();
        });
    });
    try {
        const selected = tabs.find(tab => tab.id === sessionStorage.getItem(key));
        if (selected) bootstrap.Tab.getOrCreateInstance(selected).show();
    } catch { }
})();
