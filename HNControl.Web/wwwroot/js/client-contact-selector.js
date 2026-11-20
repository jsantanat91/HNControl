(() => {
    if (window.hnContactSelectors) return;
    function init(root) {
        if (root.dataset.ready) return;
        root.dataset.ready = 'true';
        const primary = root.querySelector('[data-contact-primary]'), copies = root.querySelector('[data-contact-copies]');
        const status = root.querySelector('[data-contact-status]'), locked = root.dataset.locked === 'true';
        let request = 0;
        function draw(data, selection) {
            primary.replaceChildren(); copies.replaceChildren();
            const ids = new Set((selection?.copyContactIds || []).map(x => x.toLowerCase()));
            const selected = (selection?.primaryContactId || '').toLowerCase();
            for (const c of data.options) {
                const text = `${c.name} · ${c.email || 'Sin correo'}`;
                const option = new Option(text, c.id); option.selected = c.id.toLowerCase() === selected;
                primary.add(option);
                if (!c.id) continue;
                const label = document.createElement('label'); label.className = 'd-flex align-items-center gap-2 small';
                const check = document.createElement('input'); check.type = 'checkbox'; check.className = 'form-check-input mt-0';
                check.name = `${root.dataset.prefix}.CopyContactIds`; check.value = c.id; check.checked = ids.has(c.id.toLowerCase());
                label.append(check, document.createTextNode(text)); copies.append(label);
            }
            if (!copies.children.length) copies.textContent = 'No hay contactos adicionales. Agrégalos en la ficha del cliente.';
            root.dataset.clientId = data.clientId;
            root.querySelector('[data-contact-manage]').href = `/Clients/Details/${data.clientId}#client-contacts`;
            primary.disabled = locked;
            sync();
            status.textContent = 'La selección se guarda con este documento. Los contactos sin correo no permiten enviar.';
        }
        function sync() {
            copies.querySelectorAll('input').forEach(c => { c.disabled = locked || c.value === primary.value; if(c.value === primary.value) c.checked = false; });
        }
        primary.addEventListener('change', sync);
        const initial = JSON.parse(root.querySelector('[data-contact-options]').textContent);
        draw(initial, JSON.parse(root.querySelector('[data-contact-selection]').textContent));
        const client = root.dataset.clientSelector ? document.querySelector(root.dataset.clientSelector) : null;
        const contract = root.dataset.contractSelector ? document.querySelector(root.dataset.contractSelector) : null;
        root.reloadContacts = async (extra = {}) => {
            const current = ++request;
            primary.replaceChildren(); copies.replaceChildren(); primary.disabled = true;
            status.textContent = 'Cargando contactos…';
            root.dataset.loading = 'true';
            try {
                const url = new URL(root.dataset.url, location.origin);
                url.searchParams.set('clientId', client?.value || root.dataset.clientId);
                if (contract?.value) url.searchParams.set('contractId', contract.value);
                Object.entries(extra).forEach(([k,v]) => url.searchParams.set(k,v));
                const response = await fetch(url, { headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error();
                const data = await response.json();
                if (current !== request) return;
                draw(data, data.selection); root.dataset.loading = 'false';
            } catch { if (current === request) { status.textContent = 'No se pudieron cargar los contactos. Vuelve a seleccionar el cliente o recarga la página.'; root.dataset.loading = 'error'; } }
        };
        client?.addEventListener('change', () => root.reloadContacts({contractId:''}));
        contract?.addEventListener('change', () => root.reloadContacts());
        root.closest('form')?.addEventListener('submit', e => {
            if (root.dataset.loading && root.dataset.loading !== 'false') { e.preventDefault(); status.textContent = 'Espera a que se carguen los contactos antes de continuar.'; }
        });
    }
    window.hnContactSelectors = { init };
    const start = () => document.querySelectorAll('[data-contact-selector]').forEach(init);
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
