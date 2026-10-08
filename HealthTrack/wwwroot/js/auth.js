(() => {
    document.querySelectorAll('[data-password]').forEach(button => {
        button.hidden = false;
        const input = document.getElementById(button.dataset.password);
        const label = document.querySelector('label[for="' + input.id + '"]').textContent.toLowerCase();
        button.setAttribute('aria-label', 'Show ' + label);
        button.addEventListener('click', () => {
            const visible = input.type === 'password';
            input.type = visible ? 'text' : 'password';
            button.textContent = visible ? 'Hide' : 'Show';
            button.setAttribute('aria-label', (visible ? 'Hide ' : 'Show ') + label);
            button.setAttribute('aria-pressed', String(visible));
        });
    });
    document.querySelectorAll('.input-validation-error').forEach(input => input.setAttribute('aria-invalid', 'true'));
    const errors = document.querySelector('.validation-summary-errors');
    if (errors) errors.focus();
    document.querySelectorAll('[data-auth-form]').forEach(form => {
        form.addEventListener('submit', () => {
            const button = form.querySelector('[type="submit"]');
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
        });
    });
    window.addEventListener('pageshow', () => {
        document.querySelectorAll('[data-auth-form] [type="submit"]').forEach(button => {
            button.disabled = false;
            button.removeAttribute('aria-busy');
        });
    });
})();

