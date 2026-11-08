(() => {
  'use strict';
  const root = document.querySelector('.service-inventory');
  if (!root) return;
  const carrier = root.querySelector('[data-carrier-selector]');
  const accounts = root.querySelector('[data-inventory-account]');
  const otherName = root.querySelector('[name="Input.OtherCarrierName"]');
  const plan = root.querySelector('[data-plan-selector]');
  const connection = root.querySelector('[data-connection-selector]');
  const support = root.querySelector('[data-support-input]');
  const supportByProvider = new Map();
  let previousProvider = carrier?.value;
  if (support && previousProvider !== 'starlink') supportByProvider.set(previousProvider, support.value);

  function updateAccountHint() {
    const option = accounts?.selectedOptions[0];
    const hint = root.querySelector('#inventory-account-hint');
    if (hint) hint.textContent = option?.dataset.accountNumber ? `Número de cuenta: ${option.dataset.accountNumber}` : 'Opcional. Puedes vincular una cuenta después.';
  }

  function filterProviderOptions(select, isStarlink) {
    if (!select) return;
    const value = select.value;
    const options = Array.from(select.options);
    options.forEach(option => {
      const kind = option.dataset.providerKind;
      const allowed = !option.value || (kind === 'legacy'
        ? option.dataset.originalProvider === carrier.value
        : kind === (isStarlink ? 'starlink' : 'other'));
      option.hidden = !allowed;
      option.disabled = !allowed;
    });
    // A legacy value can also occur in another provider's options. Select the
    // enabled option by its index so the duplicate cannot replace it silently.
    const index = options.findIndex(option => option.value === value && !option.disabled);
    select.selectedIndex = index >= 0 ? index : 0;
  }

  function updateCarrier() {
    if (!carrier) return;
    const isStarlink = carrier.value === 'starlink';
    if (support && previousProvider !== carrier.value) {
      if (previousProvider !== 'starlink') supportByProvider.set(previousProvider, support.value);
      support.value = isStarlink ? 'Online' : (supportByProvider.get(carrier.value) || '');
    }
    if (support) {
      if (isStarlink) support.value = 'Online';
      support.readOnly = isStarlink;
      support.setAttribute('aria-readonly', String(isStarlink));
    }
    previousProvider = carrier.value;
    root.querySelectorAll('[data-provider-choice]').forEach(button => {
      const selected = button.dataset.providerChoice === carrier.value;
      button.classList.toggle('is-selected', selected);
      button.setAttribute('aria-pressed', String(selected));
    });
    root.querySelectorAll('[data-other-carrier]').forEach(el => { el.hidden = carrier.value !== 'otro'; });
    root.querySelectorAll('[data-starlink-help]').forEach(el => { el.hidden = !isStarlink; });
    root.querySelectorAll('[data-suggest-plan]').forEach(button => { button.hidden = isStarlink; });
    filterProviderOptions(plan, isStarlink);
    filterProviderOptions(connection, isStarlink);
    if (connection) {
      if (isStarlink) {
        connection.value = 'Satelital';
        connection.options[0].disabled = true;
      }
      connection.setAttribute('aria-readonly', String(isStarlink));
      connection.classList.toggle('is-derived', isStarlink);
    }
    const model = root.querySelector('[name="Input.DeviceModel"]');
    if (model) isStarlink ? model.setAttribute('list', 'starlink-models') : model.removeAttribute('list');
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
  root.querySelectorAll('[data-provider-choice]').forEach(button => {
    button.addEventListener('click', () => {
      if (!carrier || carrier.value === button.dataset.providerChoice) return;
      carrier.value = button.dataset.providerChoice;
      carrier.dispatchEvent(new Event('change', { bubbles: true }));
    });
  });
  root.querySelectorAll('[data-suggest-plan]').forEach(button => {
    button.addEventListener('click', () => {
      if (!plan) return;
      const index = Array.from(plan.options).findIndex(option => !option.disabled && option.value === button.dataset.suggestPlan);
      if (index >= 0) {
        plan.selectedIndex = index;
        plan.dispatchEvent(new Event('change', { bubbles: true }));
        plan.focus();
      }
    });
  });
  accounts?.addEventListener('change', () => {
    const option = accounts.selectedOptions[0];
    if (carrier?.value === 'otro' && option?.value && otherName) otherName.value = option.dataset.otherName || '';
    updateAccountHint();
  });
  updateCarrier();

  const networkToggle = root.querySelector('[data-network-toggle]');
  const networkFields = root.querySelector('[data-network-fields]');
  function updateNetworkFields() {
    if (!networkToggle || !networkFields) return;
    networkFields.hidden = !networkToggle.checked;
    networkToggle.setAttribute('aria-expanded', String(networkToggle.checked));
    networkFields.querySelectorAll('input, select, textarea').forEach(input => { input.disabled = !networkToggle.checked; });
  }
  networkToggle?.addEventListener('change', updateNetworkFields);
  updateNetworkFields();

  const start = root.querySelector('[name="Input.ContractStartsOn"]');
  const months = root.querySelector('[name="Input.ContractMonths"]');
  const end = root.querySelector('[data-contract-end]');
  const endHint = root.querySelector('[data-contract-end-hint]');
  const formatDate = value => value.split('-').reverse().join('/');
  function updateContractEnd() {
    if (!start || !months || !end) return;
    if (!start.value || !months.value) {
      const keepStoredEnd = end.dataset.storedEnd && start.value === (end.dataset.originalStart || '') && months.value === (end.dataset.originalMonths || '');
      end.textContent = keepStoredEnd ? formatDate(end.dataset.storedEnd) : (start.value || months.value ? 'Completa inicio y plazo' : 'Sin plazo definido');
      if (endHint) endHint.textContent = keepStoredEnd
        ? 'Fecha registrada anteriormente. Completa inicio y plazo para recalcularla.'
        : 'Se calcula automáticamente con el inicio y el plazo.';
      return;
    }
    const [year, month, day] = start.value.split('-').map(Number);
    const term = Number(months.value);
    if (!start.validity.valid || !Number.isInteger(term) || term < 1 || term > 240 || year < 1 || year > 9999) {
      end.textContent = 'Revisa el inicio y el plazo';
      return;
    }
    const date = new Date(0);
    date.setUTCFullYear(year, month - 1 + term, 1);
    if (date.getUTCFullYear() > 9999) {
      end.textContent = 'El plazo supera el año 9999';
      return;
    }
    const lastDay = new Date(0);
    lastDay.setUTCFullYear(date.getUTCFullYear(), date.getUTCMonth() + 1, 0);
    const last = lastDay.getUTCDate();
    date.setUTCDate(Math.min(day, last));
    end.textContent = formatDate(date.toISOString().slice(0, 10));
    if (endHint) endHint.textContent = 'Calculado automáticamente con el inicio y el plazo.';
  }
  [start, months].forEach(input => {
    input?.addEventListener('input', updateContractEnd);
    input?.addEventListener('change', updateContractEnd);
  });
  updateContractEnd();

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
