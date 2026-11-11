(() => {
    'use strict';
    const form = document.getElementById('transfer-form');
    if (!form) return;
    const body = document.getElementById('transfer-lines');
    const feedback = document.getElementById('transfer-feedback');
    const source = form.querySelector('#FromWarehouseId');
    const condition = form.querySelector('#ItemCondition');
    let products = JSON.parse(document.getElementById('transfer-products').textContent);
    let stockRequest, previewRequest, stockVersion = 0, previewVersion = 0, timer, stockReady = true;
    const orderId = form.dataset.orderId || '';
    const message = (text, style = 'warning') => {
        const item = document.createElement('div');
        item.className = `alert alert-${style}`; item.textContent = text; feedback.append(item);
    };
    function reindex() {
        [...body.rows].forEach((row, i) => {
            row.querySelector('input[type=hidden]').name = `Lines[${i}].TransferLineId`;
            row.querySelector('select').name = `Lines[${i}].ProductId`;
            row.querySelector('.transfer-quantity').name = `Lines[${i}].Quantity`;
        });
    }
    function refreshRow(row, rebuild = false) {
        const select = row.querySelector('select'), selected = select.value;
        if (rebuild) {
            select.replaceChildren(new Option('Chọn sản phẩm', ''));
            for (const p of products) select.add(new Option(`${p.productCode} — ${p.productName} (${p.unit})`, p.productId));
            if (selected && !products.some(p => String(p.productId) === selected)) select.add(new Option('Sản phẩm không còn hợp lệ — hãy chọn lại', selected));
            select.value = selected;
        }
        const available = products.find(p => String(p.productId) === select.value)?.unreservedQuantity;
        row.querySelector('.transfer-available').textContent = stockReady ? (available ?? 0).toLocaleString('vi-VN') : 'Đang tải…';
        const qty = row.querySelector('.transfer-quantity');
        const duplicate = [...body.querySelectorAll('select')].filter(s => s.value && s.value === select.value).length > 1;
        select.setCustomValidity(duplicate ? 'Sản phẩm đã có trong lệnh. Hãy gộp số lượng vào một dòng.' : '');
        qty.setCustomValidity(qty.value && !Number.isInteger(Number(qty.value)) ? 'Số lượng phải là số nguyên.' : '');
        row.classList.toggle('table-warning', stockReady && Number(qty.value) > (available ?? 0));
    }
    function schedulePreview() {
        clearTimeout(timer); previewRequest?.abort(); ++previewVersion;
        timer = setTimeout(preview, 500);
    }
    async function loadStock() {
        stockRequest?.abort(); const version = ++stockVersion;
        stockReady = false; [...body.rows].forEach(row => refreshRow(row)); schedulePreview();
        if (!source.value) { products = []; stockReady = true; [...body.rows].forEach(row => refreshRow(row, true)); return; }
        stockRequest = new AbortController();
        try {
            const url = new URL(form.dataset.productsUrl, location.origin);
            url.search = new URLSearchParams({ warehouseId: source.value, itemCondition: condition.value, transferOrderId: orderId });
            const response = await fetch(url, { signal: stockRequest.signal, headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error();
            const data = await response.json(); if (version !== stockVersion) return;
            products = data; stockReady = true; [...body.rows].forEach(row => refreshRow(row, true));
        } catch (error) {
            if (error.name === 'AbortError' || version !== stockVersion) return;
            products = []; [...body.rows].forEach(row => { row.querySelector('.transfer-available').textContent = 'Chưa tải được'; });
            feedback.replaceChildren(); message('Chưa tải được tồn nguồn. Chọn lại kho/tình trạng để thử lại; máy chủ vẫn kiểm tra dữ liệu mới nhất khi lưu.');
        }
    }
    async function preview() {
        if (!source.value || !form.querySelector('#ToWarehouseId').value || !body.rows.length) return;
        const version = ++previewVersion; previewRequest = new AbortController();
        const data = new FormData(form); data.set('IsRelease', 'true');
        if (orderId) data.set('transferOrderId', orderId);
        try {
            const response = await fetch(form.dataset.previewUrl, { method: 'POST', body: data, signal: previewRequest.signal, headers: { Accept: 'application/json' } });
            const result = await response.json(); if (version !== previewVersion) return;
            feedback.replaceChildren();
            if (!response.ok) { message(result.message || 'Không kiểm tra được lệnh. Hãy tải lại trang.', 'danger'); return; }
            if (result.currentCapacity) message(result.currentCapacity, result.warnings.includes(result.currentCapacity) ? 'warning' : 'info');
            if (result.forecastCapacity) message(result.forecastCapacity, result.errors.includes(result.forecastCapacity) ? 'warning' : 'info');
            for (const text of new Set(result.warnings))
                if (text !== result.currentCapacity && text !== result.forecastCapacity) message(text);
            for (const text of new Set(result.errors))
                if (text !== result.currentCapacity && text !== result.forecastCapacity) message(`Cần xử lý trước khi phát hành: ${text}`);
        } catch (error) {
            if (error.name !== 'AbortError' && version === previewVersion) { feedback.replaceChildren(); message('Không lấy được dự báo. Máy chủ sẽ kiểm tra lại toàn bộ điều kiện khi lưu.'); }
        }
    }
    body.addEventListener('click', event => {
        if (!event.target.closest('.transfer-remove') || body.rows.length <= 1) return;
        event.target.closest('tr').remove(); reindex(); [...body.rows].forEach(row => refreshRow(row)); schedulePreview();
    });
    document.getElementById('transfer-add')?.addEventListener('click', () => {
        if (body.rows.length >= 100) return;
        const row = document.createElement('tr');
        row.innerHTML = '<td><input type="hidden" value="0"><select class="form-select transfer-product" required></select></td><td><input class="form-control transfer-quantity" type="number" min="1" max="1000000" step="1" value="1" required></td><td class="transfer-available"></td><td><button type="button" class="btn btn-outline-danger transfer-remove" aria-label="Xóa dòng sản phẩm">Xóa</button></td>';
        body.append(row); reindex(); refreshRow(row, true); schedulePreview(); row.querySelector('select').focus();
    });
    body.addEventListener('input', () => { [...body.rows].forEach(row => refreshRow(row)); schedulePreview(); });
    source.addEventListener('change', loadStock); condition.addEventListener('change', loadStock);
    form.addEventListener('change', schedulePreview);
    // Set date requirements BEFORE native validation. Draft never inherits Release's required flags.
    form.addEventListener('click', event => {
        const button = event.target.closest('button[type=submit]');
        if (!button) return;
        const release = form.dataset.released === 'true' || button.value === 'true';
        for (const id of ['DispatchDate', 'ExpectedDate']) form.querySelector(`#${id}`).required = release;
    });
    let submitting = false;
    form.addEventListener('submit', event => {
        if (submitting) { event.preventDefault(); return; }
        reindex(); [...body.rows].forEach(row => refreshRow(row));
        if (!form.reportValidity()) { event.preventDefault(); return; }
        submitting = true;
    });
    [...body.rows].forEach(row => refreshRow(row)); schedulePreview();
})();
