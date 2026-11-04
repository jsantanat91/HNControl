(() => {
    'use strict';
    const client = document.getElementById('clientSelect');
    const project = document.getElementById('projectSelect');
    const contract = document.getElementById('contractSelect');
    const status = document.getElementById('relatedDataStatus');
    if (!client || !contract || !status) return;
    const form = client.closest('form');
    const retry = document.getElementById('relatedDataRetry');
    const context = document.getElementById('contractContext');
    const inheritProject = document.getElementById('useContractProject');
    const useTitle = document.getElementById('useContractTitle');
    let choices = JSON.parse(document.getElementById('contractOptions')?.textContent || '[]');
    let request, generation = 0, busy = false, failed = false, lastMode = 'client';
    let picker;
    if (contract.multiple) {
        picker = document.createElement('div');
        picker.className = 'hn-contract-picker';
        contract.after(picker);
        contract.hidden = true;
    }
    function renderPicker() {
        if (!picker) return;
        picker.replaceChildren();
        if (!contract.options.length) return;
        const search = document.createElement('input');
        search.type = 'search'; search.className = 'form-control mb-2';
        search.placeholder = 'Buscar contrato o sucursal…'; search.setAttribute('aria-label', 'Buscar contratos del cliente');
        const list = document.createElement('div'); list.className = 'hn-contract-picker-list';
        [...contract.options].forEach(option => {
            const label = document.createElement('label'); label.className = 'hn-contract-choice';
            const check = document.createElement('input'); check.type = 'checkbox'; check.className = 'form-check-input';
            check.checked = option.selected;
            check.addEventListener('change', () => { option.selected = check.checked; contract.dispatchEvent(new Event('change')); });
            const text = document.createElement('span'); text.textContent = option.text;
            label.append(check, text); list.append(label);
        });
        search.addEventListener('input', () => {
            const query = search.value.trim().toLocaleLowerCase('es');
            [...list.children].forEach(label => label.hidden = !label.textContent.toLocaleLowerCase('es').includes(query));
        });
        picker.append(search, list);
    }
    function show(message, error = false) {
        status.textContent = message; status.classList.toggle('is-error', error);
    }
    function replaceOptions(select, items, selected = []) {
        const placeholder = !select.multiple ? select.querySelector('option[value=""]')?.cloneNode(true) : null;
        select.replaceChildren();
        if (placeholder) select.append(placeholder);
        items.forEach(item => {
            const option = new Option(item.text, item.id, false, selected.includes(item.id));
            select.append(option);
        });
        if (!select.multiple && !items.some(item => selected.includes(item.id))) select.value = '';
    }
    function renderContext() {
        const selected = choices.find(item => item.id === contract.value);
        if (context) context.textContent = selected?.address ? `Lugar del servicio: ${selected.address}` : '';
        if (inheritProject) inheritProject.hidden = !selected?.projectId || !!project?.value;
        if (useTitle) useTitle.hidden = !selected;
        if (picker && !busy && !failed) show(contract.options.length
            ? `${contract.selectedOptions.length} de ${contract.options.length} contratos seleccionados. Sus datos estarán disponibles en el proyecto.`
            : client.value ? 'Este cliente no tiene contratos registrados. Puedes crear el proyecto sin vincular contratos.' : 'Selecciona un cliente para ver sus contratos.');
    }
    async function read(handler, clientId, projectId, signal) {
        const url = new URL(window.location.href);
        url.search = ''; // Route segment (edit ID) is retained; no old preselection parameters.
        url.searchParams.set('handler', handler); url.searchParams.set('clientId', clientId);
        if (projectId) url.searchParams.set('projectId', projectId);
        const response = await fetch(url, { signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('No se pudieron consultar los datos.');
        const items = await response.json();
        if (!Array.isArray(items)) throw new Error('Respuesta no válida.');
        return items;
    }
    async function load(mode) {
        request?.abort(); request = new AbortController();
        const version = ++generation;
        lastMode = mode; failed = false; busy = true; retry.hidden = true;
        const previous = mode === 'project' ? [...contract.selectedOptions].map(option => option.value) : [];
        replaceOptions(contract, []); choices = []; renderPicker(); renderContext();
        if (mode === 'client' && project) replaceOptions(project, []);
        contract.disabled = true; if (project) project.disabled = true;
        show(client.value ? 'Consultando datos del cliente…' : 'Selecciona un cliente para continuar.');
        try {
            if (client.value) {
                const [projects, contracts] = await Promise.all([
                    project && mode === 'client' ? read('Projects', client.value, null, request.signal) : Promise.resolve(null),
                    read('Contracts', client.value, project?.value, request.signal)
                ]);
                if (version !== generation) return;
                if (projects) replaceOptions(project, projects);
                choices = contracts; replaceOptions(contract, contracts, previous);
                show(contracts.length ? 'Datos del cliente actualizados.' : 'No hay contratos disponibles para esta selección.');
            }
        } catch (error) {
            if (version !== generation || error.name === 'AbortError') return;
            failed = true; retry.hidden = false;
            show('No fue posible cargar los datos. Reintenta antes de guardar para conservar las relaciones correctas.', true);
        } finally {
            if (version === generation) {
                busy = false; contract.disabled = failed;
                if (project) project.disabled = failed;
                renderPicker(); renderContext();
            }
        }
    }
    client.addEventListener('change', () => load('client'));
    project?.addEventListener('change', () => load('project'));
    contract.addEventListener('change', renderContext);
    retry.addEventListener('click', () => load(lastMode));
    inheritProject?.addEventListener('click', () => {
        const selected = choices.find(item => item.id === contract.value);
        if (selected?.projectId && [...project.options].some(option => option.value === selected.projectId)) {
            project.value = selected.projectId; load('project');
        }
    });
    useTitle?.addEventListener('click', () => {
        const selected = choices.find(item => item.id === contract.value);
        const title = document.getElementById('Input_Title');
        if (!selected || !title) return;
        if (title.value.trim()) { show('El título ya tiene información; se conserva lo que capturaste.'); return; }
        title.value = selected.label;
        title.dispatchEvent(new Event('input', { bubbles: true }));
        show('Nombre del servicio copiado al título. Agrega el trabajo que se realizará.');
    });
    form.addEventListener('submit', event => {
        if (!busy && !failed) return;
        event.preventDefault(); show(busy ? 'Espera a que terminen de cargar los datos del cliente.' : 'Reintenta la consulta de datos antes de guardar.', true);
        status.scrollIntoView({ block: 'center', behavior: 'smooth' });
    });
    // Initial selections are rendered by the server, including after validation errors.
    renderPicker(); renderContext();
})();
