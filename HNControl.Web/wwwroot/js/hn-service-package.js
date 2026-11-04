(() => {
    'use strict';
    const select = document.getElementById('Input_ServicePackageId');
    const button = document.getElementById('useServicePackage');
    const feedback = document.getElementById('packageFeedback');
    if (!select || !button || !feedback) return;
    const packages = JSON.parse(document.getElementById('servicePackageOptions')?.textContent || '[]');
    const selected = () => packages.find(item => item.id === select.value);
    function preview() {
        const item = selected();
        button.hidden = !item;
        feedback.textContent = item ? `${item.name}${item.price != null ? ' · Importe de catálogo: ' + item.price.toLocaleString('es-MX', { style: 'currency', currency: 'MXN' }) : ' · Importe por definir'}. Los campos que ya tienen información se conservan.` : '';
    }
    button.addEventListener('click', () => {
        const item = selected();
        if (!item) return;
        const updated = [];
        [['Input_Label', item.name, 'nombre'], ['Input_MonthlyAmount', item.price, 'importe']].forEach(([id, value, label]) => {
            const field = document.getElementById(id);
            if (!field || field.value.trim() || value == null) return;
            field.value = value;
            field.dispatchEvent(new Event('input', { bubbles: true }));
            field.dispatchEvent(new Event('change', { bubbles: true }));
            updated.push(label);
        });
        const type = [...document.querySelectorAll('.service-type-toggle')].find(toggle => toggle.value === item.type);
        if (type && !type.checked) {
            type.checked = true;
            type.dispatchEvent(new Event('change', { bubbles: true }));
            updated.push('tipo de servicio');
        }
        feedback.textContent = updated.length ? `Se completó: ${updated.join(', ')}. Revisa los servicios incluidos y la modalidad antes de guardar.` : 'Los campos ya tienen información; se conservó lo que capturaste.';
    });
    select.addEventListener('change', preview);
    preview();
})();
