(() => {
    'use strict';
    const form = document.getElementById('transfer-form');
    if (!form) return;

    const body = document.getElementById('transfer-lines');
    const feedback = document.getElementById('transfer-feedback');
    const source = form.querySelector('#FromWarehouseId');
    const destination = form.querySelector('#ToWarehouseId');
    const condition = form.querySelector('#ItemCondition');
    const dispatchInput = form.querySelector('#DispatchDate');
    const expectedInput = form.querySelector('#ExpectedDate');
    const noteInput = form.querySelector('#Note');
    const totalQtyDisplay = document.getElementById('totalQtyDisplay');

    let products = JSON.parse(document.getElementById('transfer-products').textContent || '[]');
    let stockRequest, previewRequest, stockVersion = 0, previewVersion = 0, timer;
    let stockReady = true;
    const orderId = form.dataset.orderId || '';
    let pendingRelease = form.dataset.released === 'true';

    function localTodayIso() {
        const now = new Date();
        const utc = now.getTime() + now.getTimezoneOffset() * 60000;
        const vn = new Date(utc + 7 * 3600000);
        const y = vn.getFullYear();
        const m = String(vn.getMonth() + 1).padStart(2, '0');
        const d = String(vn.getDate()).padStart(2, '0');
        return `${y}-${m}-${d}`;
    }

    function setFieldError(fieldName, message) {
        if (!fieldName) return;
        const input = form.querySelector(`[name="${CSS.escape(fieldName)}"]`);
        const span = form.querySelector(`[data-valmsg-for="${CSS.escape(fieldName)}"]`);
        if (message) {
            input?.classList.add('input-validation-error');
            if (span) {
                span.textContent = message;
                span.classList.remove('field-validation-valid');
                span.classList.add('field-validation-error');
            }
        } else {
            input?.classList.remove('input-validation-error');
            if (span) {
                span.textContent = '';
                span.classList.add('field-validation-valid');
                span.classList.remove('field-validation-error');
            }
        }
    }

    function clearAllErrors() {
        form.querySelectorAll('.input-validation-error').forEach(el => el.classList.remove('input-validation-error'));
        form.querySelectorAll('[data-valmsg-for]').forEach(span => {
            span.textContent = '';
            span.classList.add('field-validation-valid');
            span.classList.remove('field-validation-error');
        });
    }

    function renderNotice(text, style = 'warning') {
        const item = document.createElement('div');
        item.className = `transfer-notice transfer-notice-${style}`;
        const icon = document.createElement('i');
        icon.className = style === 'danger'
            ? 'bi bi-exclamation-triangle-fill flex-shrink-0'
            : 'bi bi-exclamation-triangle flex-shrink-0';
        icon.setAttribute('aria-hidden', 'true');
        const span = document.createElement('span');
        span.textContent = text;
        item.append(icon, span);
        feedback.append(item);
    }

    function updateTotalQty() {
        if (!totalQtyDisplay) return;
        let sum = 0;
        for (const row of body.rows) {
            const val = Number(row.querySelector('.transfer-quantity')?.value);
            if (Number.isInteger(val) && val > 0) sum += val;
        }
        totalQtyDisplay.textContent = sum.toLocaleString('vi-VN');
    }

    function reindex() {
        const rows = [...body.rows];
        rows.forEach((row, i) => {
            const numEl = row.querySelector('.transfer-row-num');
            if (numEl) numEl.textContent = String(i + 1);

            const hidden = row.querySelector('input[type=hidden]');
            const select = row.querySelector('select.transfer-product');
            const qty = row.querySelector('input.transfer-quantity');
            const prodSpan = select?.closest('td')?.querySelector('.transfer-field-error');
            const qtySpan = qty?.closest('td')?.querySelector('.transfer-field-error');
            const removeBtn = row.querySelector('.transfer-remove');

            if (hidden) hidden.name = `Lines[${i}].TransferLineId`;
            if (select) {
                select.name = `Lines[${i}].ProductId`;
                select.id = `Lines_${i}__ProductId`;
            }
            if (prodSpan) prodSpan.setAttribute('data-valmsg-for', `Lines[${i}].ProductId`);
            if (qty) {
                qty.name = `Lines[${i}].Quantity`;
                qty.id = `Lines_${i}__Quantity`;
            }
            if (qtySpan) qtySpan.setAttribute('data-valmsg-for', `Lines[${i}].Quantity`);
            if (removeBtn) removeBtn.disabled = rows.length <= 1;
        });
    }

    function refreshRow(row, rebuild = false) {
        const select = row.querySelector('select.transfer-product');
        const unitEl = row.querySelector('.transfer-unit');
        const stockEl = row.querySelector('.transfer-available');
        const qtyInput = row.querySelector('input.transfer-quantity');
        if (!select) return;

        const selected = select.value;
        if (rebuild) {
            select.replaceChildren(new Option('-- Chọn sản phẩm --', ''));
            for (const p of products) {
                select.add(new Option(`[${p.productCode}] ${p.productName}`, p.productId));
            }
            if (selected && !products.some(p => String(p.productId) === selected)) {
                select.add(new Option('Sản phẩm không còn khả dụng', selected));
            }
            select.value = selected;
        }

        const product = products.find(p => String(p.productId) === select.value);
        if (unitEl) unitEl.textContent = product?.unit || '—';

        if (stockEl) {
            stockEl.classList.remove('badge-stock', 'badge-stock-ok', 'badge-stock-zero');
            if (!stockReady) {
                stockEl.textContent = 'Đang tải…';
                stockEl.classList.add('badge-stock');
            } else if (!select.value) {
                stockEl.textContent = '—';
                stockEl.classList.add('badge-stock');
            } else {
                const available = product?.unreservedQuantity ?? 0;
                const qtyVal = Number(qtyInput?.value || 0);
                stockEl.textContent = available.toLocaleString('vi-VN');
                stockEl.classList.add(available > 0 && qtyVal <= available ? 'badge-stock-ok' : 'badge-stock-zero');
            }
        }
    }

    function validateForm(isRelease) {
        clearAllErrors();
        let valid = true;

        const fromId = source.value.trim();
        const toId = destination.value.trim();
        const condVal = condition.value.trim();
        const dispatchVal = dispatchInput?.value.trim() || '';
        const expectedVal = expectedInput?.value.trim() || '';
        const todayIso = localTodayIso();

        if (!fromId) {
            setFieldError('FromWarehouseId', 'Vui lòng chọn kho nguồn.');
            valid = false;
        }
        if (!toId) {
            setFieldError('ToWarehouseId', 'Vui lòng chọn kho đích.');
            valid = false;
        } else if (fromId && fromId === toId) {
            setFieldError('ToWarehouseId', 'Kho đích phải khác kho nguồn.');
            valid = false;
        }

        if (condVal !== 'GOOD' && condVal !== 'DAMAGED') {
            setFieldError('ItemCondition', 'Vui lòng chọn tình trạng hàng hóa.');
            valid = false;
        }

        if (isRelease && !dispatchVal) {
            setFieldError('DispatchDate', 'Vui lòng nhập ngày dự kiến gửi.');
            valid = false;
        }
        if (isRelease && !expectedVal) {
            setFieldError('ExpectedDate', 'Vui lòng nhập ngày dự kiến đến.');
            valid = false;
        } else if (expectedVal && expectedVal < todayIso) {
            setFieldError('ExpectedDate', 'Ngày dự kiến đến không được nhỏ hơn ngày hôm nay.');
            valid = false;
        } else if (dispatchVal && expectedVal && expectedVal < dispatchVal) {
            setFieldError('ExpectedDate', 'Ngày dự kiến đến phải từ ngày dự kiến gửi trở đi.');
            valid = false;
        }

        if (noteInput && noteInput.value.length > 1000) {
            setFieldError('Note', 'Ghi chú tối đa 1.000 ký tự.');
            valid = false;
        }

        const seen = new Set();
        [...body.rows].forEach((row, i) => {
            const select = row.querySelector('select.transfer-product');
            const qtyInput = row.querySelector('input.transfer-quantity');
            const prodVal = select?.value.trim() || '';
            const qtyRaw = qtyInput?.value.trim() || '';
            const qtyNum = Number(qtyRaw);

            if (!prodVal) {
                setFieldError(`Lines[${i}].ProductId`, 'Vui lòng chọn sản phẩm.');
                valid = false;
            } else if (seen.has(prodVal)) {
                setFieldError(`Lines[${i}].ProductId`, 'Sản phẩm bị trùng, vui lòng gộp số lượng vào một dòng.');
                valid = false;
            } else {
                seen.add(prodVal);
            }

            if (!qtyRaw || !Number.isInteger(qtyNum) || qtyNum < 1 || qtyNum > 1000000) {
                setFieldError(`Lines[${i}].Quantity`, 'Số lượng phải là số nguyên từ 1 đến 1.000.000.');
                valid = false;
            } else if (isRelease && stockReady && prodVal) {
                const product = products.find(p => String(p.productId) === prodVal);
                if (product && qtyNum > product.unreservedQuantity) {
                    setFieldError(`Lines[${i}].Quantity`, `Vượt tồn khả dụng (${product.unreservedQuantity.toLocaleString('vi-VN')}).`);
                    valid = false;
                }
            }
        });

        if (!valid) {
            const firstInvalid = form.querySelector('.input-validation-error');
            firstInvalid?.focus();
            firstInvalid?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
        return valid;
    }

    function schedulePreview() {
        clearTimeout(timer);
        previewRequest?.abort();
        ++previewVersion;
        timer = setTimeout(preview, 450);
    }

    async function loadStock() {
        stockRequest?.abort();
        const version = ++stockVersion;
        stockReady = false;
        [...body.rows].forEach(row => refreshRow(row));
        schedulePreview();

        if (!source.value) {
            products = [];
            stockReady = true;
            [...body.rows].forEach(row => refreshRow(row, true));
            return;
        }

        stockRequest = new AbortController();
        try {
            const url = new URL(form.dataset.productsUrl, location.origin);
            url.search = new URLSearchParams({
                warehouseId: source.value,
                itemCondition: condition.value,
                transferOrderId: orderId
            });
            const response = await fetch(url, { signal: stockRequest.signal, headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error();
            const data = await response.json();
            if (version !== stockVersion) return;
            products = data;
            stockReady = true;
            [...body.rows].forEach(row => refreshRow(row, true));
        } catch (error) {
            if (error.name === 'AbortError' || version !== stockVersion) return;
            products = [];
            stockReady = true;
            [...body.rows].forEach(row => refreshRow(row, true));
        }
    }

    async function preview() {
        if (!source.value || !destination.value || source.value === destination.value) {
            feedback.replaceChildren();
            return;
        }

        const hasValidLine = [...body.rows].some(row => {
            const p = row.querySelector('select.transfer-product')?.value;
            const q = Number(row.querySelector('input.transfer-quantity')?.value);
            return p && Number.isInteger(q) && q > 0;
        });

        if (!hasValidLine) {
            feedback.replaceChildren();
            return;
        }

        const version = ++previewVersion;
        previewRequest = new AbortController();
        const data = new FormData(form);
        data.set('IsRelease', 'false');
        if (orderId) data.set('transferOrderId', orderId);

        try {
            const response = await fetch(form.dataset.previewUrl, {
                method: 'POST',
                body: data,
                signal: previewRequest.signal,
                headers: { Accept: 'application/json' }
            });
            const result = await response.json();
            if (version !== previewVersion) return;
            feedback.replaceChildren();

            if (!response.ok) {
                if (result.message) renderNotice(result.message, 'danger');
                return;
            }

            // Show only genuine capacity/stock/rental issues without noisy boilerplate
            const issueMessages = new Set([...(result.errors || []), ...(result.warnings || [])]);
            for (const text of issueMessages) {
                if (text) renderNotice(text, 'warning');
            }
        } catch (error) {
            if (error.name === 'AbortError' || version !== previewVersion) return;
            feedback.replaceChildren();
        }
    }

    body.addEventListener('click', event => {
        const removeBtn = event.target.closest('.transfer-remove');
        if (!removeBtn || body.rows.length <= 1) return;
        removeBtn.closest('tr').remove();
        reindex();
        [...body.rows].forEach(row => refreshRow(row));
        updateTotalQty();
        schedulePreview();
    });

    document.getElementById('transfer-add')?.addEventListener('click', () => {
        if (body.rows.length >= 100) return;
        const row = document.createElement('tr');
        row.innerHTML =
            '<td style="text-align: center;"><span class="transfer-row-num muted fw-semibold"></span></td>' +
            '<td>' +
                '<input type="hidden" value="0">' +
                '<select class="form-select transfer-product"></select>' +
                '<span class="transfer-field-error field-validation-valid" data-valmsg-replace="true"></span>' +
            '</td>' +
            '<td style="text-align: center;"><span class="transfer-unit muted">—</span></td>' +
            '<td style="text-align: center;"><span class="stock-badge badge-stock transfer-available">—</span></td>' +
            '<td>' +
                '<input class="form-control text-end transfer-quantity" type="number" min="1" max="1000000" step="1" value="1">' +
                '<span class="transfer-field-error text-end field-validation-valid" data-valmsg-replace="true"></span>' +
            '</td>' +
            '<td style="text-align: center;">' +
                '<button type="button" class="sales-danger-icon transfer-remove" title="Xóa dòng" aria-label="Xóa dòng sản phẩm">' +
                    '<i class="bi bi-trash" aria-hidden="true"></i>' +
                '</button>' +
            '</td>';
        body.append(row);
        reindex();
        refreshRow(row, true);
        updateTotalQty();
        schedulePreview();
        row.querySelector('select')?.focus();
    });

    body.addEventListener('input', event => {
        const row = event.target.closest('tr');
        if (row) refreshRow(row);
        if (event.target.name) setFieldError(event.target.name, '');
        updateTotalQty();
        schedulePreview();
    });

    form.addEventListener('change', event => {
        const target = event.target;
        if (target.name) setFieldError(target.name, '');
        if (target === source || target === destination) {
            if (source.value && destination.value && source.value !== destination.value) {
                setFieldError('ToWarehouseId', '');
            }
        }
        if (target.classList.contains('transfer-product')) {
            const row = target.closest('tr');
            if (row) refreshRow(row);
        }
        updateTotalQty();
        schedulePreview();
    });

    source.addEventListener('change', loadStock);
    condition.addEventListener('change', loadStock);

    form.addEventListener('click', event => {
        const button = event.target.closest('button[type=submit]');
        if (!button) return;
        pendingRelease = form.dataset.released === 'true' || button.value === 'true';
    });

    let submitting = false;
    form.addEventListener('submit', event => {
        if (submitting) {
            event.preventDefault();
            return;
        }
        const submitter = event.submitter;
        const isRelease = form.dataset.released === 'true'
            || (submitter && submitter.name === 'IsRelease' ? submitter.value === 'true' : pendingRelease);
        reindex();
        if (!validateForm(isRelease)) {
            event.preventDefault();
            return;
        }
        submitting = true;
    });

    reindex();
    [...body.rows].forEach(row => refreshRow(row));
    updateTotalQty();
    schedulePreview();
})();
