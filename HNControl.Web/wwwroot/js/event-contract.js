document.addEventListener('DOMContentLoaded', () => {
    const mode = document.getElementById('Input_BillingRecurrence');
    if (!mode) return;
    const start = document.getElementById('Input_ContractStartDate');
    const end = document.getElementById('Input_ContractEndDate');
    const amount = document.getElementById('Input_MonthlyAmount');
    const term = document.getElementById('Input_ContractTermOption');
    const update = () => {
        const eventual = mode.value === 'Eventual';
        document.getElementById('contractAmountLabel').textContent = eventual
            ? 'Importe único del evento (antes de IVA)' : 'Monto del servicio (antes de IVA)';
        start.required = end.required = amount.required = eventual;
        term.closest('.col-md-3').hidden = eventual;
        if (eventual) term.value = 'Eventual';
        else if (term.value === 'Eventual') term.value = '12';
        end.min = eventual ? start.value : '';
    };
    mode.addEventListener('change', update);
    start.addEventListener('change', update);
    update();
});
