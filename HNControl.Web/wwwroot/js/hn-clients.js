(() => {
    'use strict';
    const profile = document.querySelector('[data-client-id]');
    if (profile) {
        const tabs = [...profile.querySelectorAll('.hn-client-tabs [role="tab"]')];
        const key = 'hn.client.tab.' + profile.dataset.clientId;
        tabs.forEach((tab, index) => {
            tab.tabIndex = tab.classList.contains('active') ? 0 : -1;
            tab.addEventListener('shown.bs.tab', () => {
                tabs.forEach(item => item.tabIndex = item === tab ? 0 : -1);
                try { sessionStorage.setItem(key, tab.id); } catch { /* Storage may be disabled. */ }
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
            const saved = tabs.find(tab => tab.id === sessionStorage.getItem(key));
            if (saved) bootstrap.Tab.getOrCreateInstance(saved).show();
        } catch { /* Start with Contracts if storage is unavailable. */ }
    }
    document.querySelectorAll('[data-copy-target]:not([data-copy-source])').forEach(button => {
        button.addEventListener('click', async () => {
            const input = document.getElementById(button.dataset.copyTarget);
            const container = button.closest('.card-body');
            if (!input || !container) return;
            let status = container.querySelector('.hn-copy-status');
            if (!status) {
                status = document.createElement('span');
                status.className = 'hn-copy-status';
                status.setAttribute('role', 'status');
                container.append(status);
            }
            try {
                await navigator.clipboard.writeText(input.value);
                status.textContent = 'Copiado al portapapeles.';
            } catch {
                status.textContent = 'No se pudo copiar. Revisa los permisos del portapapeles.';
            }
        });
    });
    document.querySelectorAll('[data-password-toggle]').forEach(button => {
        button.addEventListener('click', () => {
            const input = document.getElementById(button.dataset.passwordToggle);
            if (!input) return;
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(show));
            button.setAttribute('aria-label', show ? 'Ocultar contraseña' : 'Mostrar contraseña');
        });
    });
})();
