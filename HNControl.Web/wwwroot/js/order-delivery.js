(() => {
    const list = document.getElementById('equipment-list');
    const add = document.getElementById('add-equipment');
    const router = document.getElementById('Input_HasRouter');
    const sync = () => {
        document.getElementById('equipment-empty').hidden = list.children.length > 0;
        add.disabled = list.children.length >= 20;
        Array.from(list.children).forEach((row, i) => {
            row.querySelectorAll('input,select').forEach(field => {
                const suffix = field.name.split('.').pop();
                field.name = `Input.Items[${i}].${suffix}`;
                const oldId = field.id;
                field.id = `delivery-equipment-${i}-${suffix}`;
                const label = Array.from(row.querySelectorAll('label')).find(l => l.htmlFor === oldId);
                if (label) label.htmlFor = field.id;
            });
        });
    };
    router.addEventListener('change', () => { document.getElementById('router-fields').hidden = !router.checked; });
    add.addEventListener('click', () => {
        if (list.children.length >= 20) return;
        list.append(document.getElementById('equipment-template').content.cloneNode(true));
        sync(); list.lastElementChild.querySelector('input').focus();
    });
    list.addEventListener('click', e => { const button = e.target.closest('[data-remove-equipment]'); if (button) { button.closest('.delivery-equipment').remove(); sync(); } });
    sync();
})();
