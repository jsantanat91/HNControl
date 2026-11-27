(() => {
    const type = document.getElementById('Input_Type');
    if (!type) return;
    const title = document.getElementById('Input_Title');
    const description = document.getElementById('Input_Description');
    const label = document.querySelector('label[for="Input_Description"]');
    const clients = document.getElementById('clientSelect');
    const inactive = new Set(JSON.parse(document.getElementById('inactiveClientIds').textContent));
    const toggle = document.getElementById('collectionOptions');
    const contacts = document.getElementById('collectionContacts');
    const contactHint = contacts?.querySelector('p');
    const contactLabel = contacts?.querySelector('label.form-label')?.firstChild;
    const originalHint = contactHint?.textContent;
    const originalLabel = contactLabel?.textContent;
    const optional = [title.closest('.col-md-8'), document.getElementById('projectSelect').parentElement,
        document.getElementById('contractSelect').parentElement, contacts, document.getElementById('Input_StartDate').closest('section')];
    let expanded = false;
    toggle.addEventListener('click', () => { expanded = !expanded; update(); });
    function update() {
        const collection = type.value === 'RecoleccionEquipos' || type.value === '7';
        toggle.hidden = !collection;
        toggle.textContent = expanded ? 'Ocultar datos opcionales' : 'Mostrar datos opcionales';
        toggle.setAttribute('aria-expanded', String(expanded));
        for (const section of optional) if (section) section.hidden = collection && !expanded;
        if (contacts) contacts.disabled = collection && !expanded;
        if (contactHint) contactHint.textContent = collection ? 'El principal y las copias reciben los avisos de esta recolección. No se solicita firma.' : originalHint;
        if (contactLabel) contactLabel.textContent = collection ? 'Contacto principal · notificaciones' : originalLabel;
        document.getElementById('orderClientHint').textContent = collection ? 'También puedes seleccionar clientes inactivos para recuperar sus equipos.' : 'Primero selecciona cliente; luego proyecto y/o contrato.';
        document.getElementById('collectionHint').hidden = !collection;
        document.getElementById('deliveryKindSection').hidden = collection;
        document.getElementById('Input_DeliveryKind').disabled = collection;
        label.textContent = collection ? 'Dirección de recolección' : 'Descripción';
        description.required = collection;
        description.placeholder = collection ? 'Calle, número, colonia, municipio y referencias para recoger los equipos' : 'Contexto, alcance, observaciones...';
        if (collection && !title.value.trim()) title.value = 'Recolección de equipos';
        for (const option of clients.options) option.disabled = !collection && inactive.has(option.value);
        if (clients.selectedOptions[0]?.disabled) {
            clients.value = '';
            clients.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }
    type.addEventListener('change', update);
    update();
})();
