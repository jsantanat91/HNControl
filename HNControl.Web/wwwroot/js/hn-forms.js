(() => {
    'use strict';
    document.querySelectorAll('.hn-workflow-form').forEach(form => {
        form.querySelectorAll('[data-form-optional]').forEach(section => {
            const hasValue = [...section.querySelectorAll('input:not([type=hidden]), textarea, select')]
                .some(field => field.name && field.value.trim() && !field.hasAttribute('data-optional-default'));
            const hasError = section.querySelector('.input-validation-error, .field-validation-error');
            if (!hasValue && !hasError) section.open = false;
        });
        // Expand before native or unobtrusive validation focuses a hidden field.
        form.addEventListener('invalid', event => {
            event.target.closest('details')?.setAttribute('open', '');
        }, true);
        if (window.jQuery) {
            window.jQuery(form).on('invalid-form.validate', (_event, validator) => {
                validator.errorList.forEach(error => error.element.closest('details')?.setAttribute('open', ''));
            });
        }
        form.querySelectorAll('[data-copy-target]').forEach(button => {
            button.addEventListener('click', () => {
                const target = document.getElementById(button.dataset.copyTarget);
                const source = button.dataset.copySource ? document.getElementById(button.dataset.copySource)?.value : button.dataset.copyValue;
                const feedback = button.parentElement.querySelector('[data-copy-feedback]');
                if (!target) return;
                let message;
                if (target.value.trim()) message = 'El campo ya tiene información. Se conserva lo que capturaste.';
                else if (!source?.trim()) message = 'Todavía no hay datos de origen para copiar.';
                else {
                    target.value = source;
                    target.dispatchEvent(new Event('input', { bubbles: true }));
                    target.dispatchEvent(new Event('change', { bubbles: true }));
                    message = 'Dato copiado. Puedes ajustarlo antes de guardar.';
                }
                if (feedback) feedback.textContent = message;
            });
        });
    });
})();
