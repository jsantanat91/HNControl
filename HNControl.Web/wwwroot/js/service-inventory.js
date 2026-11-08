(() => {
  'use strict';
  const root = document.querySelector('.service-inventory');
  if (!root) return;
  const carrier = root.querySelector('[data-carrier-selector]');
  const accounts = root.querySelector('[data-inventory-account]');
  const otherName = root.querySelector('[name="Input.OtherCarrierName"]');
  function updateAccountHint() {
    const option = accounts?.selectedOptions[0];
    const hint = root.querySelector('#inventory-account-hint');
    if (hint) hint.textContent = option?.dataset.accountNumber ? `Número de cuenta: ${option.dataset.accountNumber}` : 'Opcional. El servicio puede registrarse sin credenciales.';
  }
  function updateCarrier() {
    if (!carrier) return;
    root.querySelectorAll('[data-other-carrier]').forEach(el => { el.hidden = carrier.value !== 'otro'; });
    root.querySelectorAll('[data-starlink-help]').forEach(el => { el.hidden = carrier.value !== 'starlink'; });
    const model = root.querySelector('[name="Input.DeviceModel"]');
    if (model) carrier.value === 'starlink' ? model.setAttribute('list', 'starlink-models') : model.removeAttribute('list');
    if (accounts) {
      Array.from(accounts.options).forEach(option => {
        const mismatch = !!option.value && option.dataset.carrier !== carrier.value;
        option.hidden = mismatch;
        option.disabled = mismatch;
      });
      if (accounts.selectedOptions[0]?.disabled) accounts.value = '';
      updateAccountHint();
    }
  }
  carrier?.addEventListener('change', updateCarrier);
  accounts?.addEventListener('change', () => {
    const option = accounts.selectedOptions[0];
    if (carrier?.value === 'otro' && option?.value && otherName) otherName.value = option.dataset.otherName || '';
    updateAccountHint();
  });
  updateCarrier();

  root.querySelector('[data-calculate-term]')?.addEventListener('click', () => {
    const start = root.querySelector('[name="Input.ContractStartsOn"]');
    const months = root.querySelector('[name="Input.ContractMonths"]');
    const end = root.querySelector('[name="Input.ContractEndsOn"]');
    if (!start.value || !months.value || !start.reportValidity() || !months.reportValidity()) return;
    const [year, month, day] = start.value.split('-').map(Number);
    const date = new Date(Date.UTC(year, month - 1 + Number(months.value), 1));
    const last = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
    date.setUTCDate(Math.min(day, last));
    end.value = date.toISOString().slice(0, 10);
    end.dispatchEvent(new Event('change', { bubbles: true }));
  });

  root.querySelectorAll('[data-copy-password]').forEach(button => {
    button.addEventListener('click', async () => {
      const status = root.querySelector('#inventory-copy-status');
      const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value;
      if (!status) return;
      status.classList.remove('is-error');
      if (!window.isSecureContext || !navigator.clipboard?.writeText) {
        status.classList.add('is-error');
        status.textContent = 'Abre HN Control mediante HTTPS y permite el acceso al portapapeles para copiar la contraseña.';
        return;
      }
      button.disabled = true;
      status.textContent = 'Copiando contraseña…';
      let payload = null;
      try {
        if (!token) throw new Error('token');
        const response = await fetch(button.dataset.copyUrl, {
          method: 'POST', credentials: 'same-origin', cache: 'no-store', redirect: 'error',
          headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Accept': 'application/json' },
          body: new URLSearchParams({ id: button.dataset.copyPassword, __RequestVerificationToken: token })
        });
        if (!response.ok) throw new Error('request');
        payload = await response.json();
        if (typeof payload.password !== 'string' || !payload.password) throw new Error('empty');
        await navigator.clipboard.writeText(payload.password);
        status.textContent = 'Contraseña copiada al portapapeles.';
      } catch {
        status.classList.add('is-error');
        status.textContent = 'No se pudo copiar. Verifica tu sesión y el permiso del portapapeles, y vuelve a intentarlo.';
      } finally {
        if (payload) payload.password = '';
        payload = null;
        button.disabled = false;
      }
    });
  });
})();
