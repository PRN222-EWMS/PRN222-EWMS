(() => {
    const eye = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/><path class="password-eye-slash" d="m3 3 18 18"/></svg>';

    document.querySelectorAll('input[type="password"]').forEach(input => {
        if (input.closest('.password-input')) return;
        const wrapper = document.createElement('div');
        wrapper.className = 'password-input';
        input.before(wrapper);
        wrapper.append(input);

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'password-toggle';
        button.innerHTML = eye;
        button.setAttribute('aria-label', 'Hiện mật khẩu');
        button.setAttribute('aria-pressed', 'false');
        button.title = 'Hiện mật khẩu';
        if (input.id) button.setAttribute('aria-controls', input.id);
        button.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(show));
            button.setAttribute('aria-label', show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
            button.title = show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu';
        });
        wrapper.append(button);
    });
})();
