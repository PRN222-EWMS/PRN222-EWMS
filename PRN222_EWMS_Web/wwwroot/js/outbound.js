(() => {
    'use strict';

    const scanForm = document.getElementById('scanSerialForm');
    let submitting = false;
    const submitForms = document.querySelectorAll('form[data-outbound-submit-once]');

    submitForms.forEach(form => form.addEventListener('submit', event => {
        if (submitting) { event.preventDefault(); return; }
        const confirmation = form.dataset.outboundConfirm;
        if (confirmation && !window.confirm(confirmation)) { event.preventDefault(); return; }
        submitting = true;
        form.setAttribute('aria-busy', 'true');
        document.querySelectorAll('form[data-outbound-submit-once] button[type="submit"], .btn-quick-pick').forEach(button => {
            if (!button.disabled) button.disabled = true;
        });
    }));

    document.querySelectorAll('.btn-quick-pick').forEach(button => button.addEventListener('click', () => {
        if (submitting || button.disabled || !scanForm) return;
        const input = scanForm.querySelector('[data-serial-input]');
        if (!input || !button.dataset.serial) return;
        input.value = button.dataset.serial;
        const method = scanForm.querySelector('[data-capture-method]');
        if (method) method.value = 'QUICK_SELECT';
        scanForm.requestSubmit();
    }));

    let selectedProduct = 'ALL';
    const serialSearch = document.getElementById('outbound-serial-search');
    const suggestedRows = [...document.querySelectorAll('.suggested-serial-row')];
    const suggestionEmpty = document.getElementById('outbound-suggestions-empty');
    const filterSuggestions = () => {
        const term = (serialSearch?.value || '').trim().toLocaleLowerCase('vi');
        let shown = 0;
        suggestedRows.forEach(row => {
            const visible = (selectedProduct === 'ALL' || row.dataset.productId === selectedProduct)
                && (row.dataset.suggestionSearch || '').toLocaleLowerCase('vi').includes(term);
            row.hidden = !visible;
            if (visible) shown++;
        });
        suggestionEmpty?.classList.toggle('d-none', shown > 0 || suggestedRows.length === 0);
    };
    const productFilters = [...document.querySelectorAll('[data-product-filter]')];
    productFilters.forEach(button => button.addEventListener('click', () => {
        selectedProduct = button.dataset.productFilter;
        productFilters.forEach(filter => {
            const active = filter === button;
            filter.classList.toggle('active', active);
            filter.setAttribute('aria-pressed', String(active));
        });
        filterSuggestions();
    }));
    serialSearch?.addEventListener('input', filterSuggestions);

    const orderSearch = document.getElementById('outbound-order-search');
    const orderCards = [...document.querySelectorAll('[data-outbound-order]')];
    orderSearch?.addEventListener('input', () => {
        const term = orderSearch.value.trim().toLocaleLowerCase('vi');
        let shown = 0;
        orderCards.forEach(card => {
            const visible = (card.dataset.search || '').toLocaleLowerCase('vi').includes(term);
            card.hidden = !visible;
            if (visible) shown++;
        });
        document.getElementById('outbound-orders-empty')?.classList.toggle('d-none', shown > 0);
    });

    // Back must refresh both the available stock and the create request token.
    window.addEventListener('pageshow', event => {
        if (event.persisted) window.location.reload();
    });
})();
