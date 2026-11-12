// Source choices belong to an SO line; Draft checks availability without holding it.
document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('salesOrderForm');
    const body = document.getElementById('linesTbody');
    if (!form || !body) return;
    const warehouse = document.getElementById('WarehouseSelect');
    const deadline = document.getElementById('RequestedDateInput');
    const replacement = form.dataset.replacement === 'true';
    const status = document.createElement('div');
    status.className = 'order-sources-status';
    status.setAttribute('role', 'status');
    body.closest('.table-wrap').before(status);
    const state = new WeakMap();
    let catalog = [], ready = false, request, version = 0;
    let initial = [];
    try { initial = JSON.parse(document.getElementById('initialSourcesJson')?.textContent || '[]'); }
    catch { /* Server validation remains authoritative if form metadata cannot be read. */ }
    const key = source => source.supplySourceId > 0 ? `S:${source.supplySourceId}` : `T:${source.transferLineId}`;
    const rows = () => Array.from(body.children).filter(row => row.tagName === 'TR');
    const productId = row => Number(row.querySelector('.product-select')?.value || row.dataset.productId || 0);
    const demand = row => Number(row.querySelector('.qty-input')?.value || 0);
    const element = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };
    const describe = source => `${source.label} · ${source.supplyState === 'PHYSICAL' ? 'Có sẵn' : 'Đến ' + (source.availableFrom || '?')}`
        + ` · Khả dụng ${source.availableQuantity}${source.warning ? ' · ' + source.warning : ''}`;
    const sourceKind = source => source.supplyState === 'PHYSICAL' ? 'Trong kho'
        : source.sourceType === 'TRANSFER_INCOMING' ? 'Chuyển kho' : 'Hàng mua';
    const dateLabel = value => value ? value.split('-').reverse().join('/') : 'Chưa có ngày';

    function init(row, choices = []) {
        if (state.has(row)) return;
        const editor = element('div', 'order-source-editor');
        editor.append(element('div', 'order-source-heading', replacement ? 'Nguồn bổ sung' : 'Phân bổ nguồn'));
        const empty = element('div', 'order-source-empty', 'Chưa chọn nguồn hàng.');
        const list = element('div', 'order-source-list');
        const footer = element('div', 'order-source-footer');
        const add = element('button', 'order-source-add', '+ Thêm nguồn');
        add.type = 'button';
        const note = element('div', 'order-source-note');
        note.setAttribute('aria-live', 'polite');
        footer.append(add, note);
        editor.append(empty, list, footer);
        (row.querySelector('.product-select')?.parentElement || row.children[1]).append(editor);
        const entry = { choices: choices.map(c => ({ key: key(c), quantity: c.quantity })), list, add, note, empty, product: productId(row) };
        state.set(row, entry);
        add.addEventListener('click', () => { entry.choices.push({ key: '', quantity: 1 }); render(row); });
        render(row);
    }

    function render(row) {
        const entry = state.get(row);
        const options = catalog.filter(s => s.productId === productId(row));
        entry.list.replaceChildren();
        entry.add.disabled = !ready || !productId(row);
        entry.empty.hidden = entry.choices.length > 0;
        entry.choices.forEach((choice, index) => {
            const wrapper = element('div', 'order-source-choice');
            const select = element('select', 'form-select form-select-sm sales-source-select');
            select.setAttribute('aria-label', 'Chọn nguồn hàng');
            select.append(new Option('-- Chọn nguồn --', ''));
            options.forEach(option => {
                const optionKey = key(option);
                const item = new Option(describe(option), optionKey, false, optionKey === choice.key);
                item.disabled = optionKey !== choice.key && (!option.canSelect || entry.choices.some((c, i) => i !== index && c.key === optionKey));
                select.append(item);
            });
            if (choice.key && !options.some(option => key(option) === choice.key))
                select.append(new Option('Nguồn đã chọn không còn khả dụng', choice.key, true, true));
            select.value = choice.key;
            const quantity = element('input', 'form-control form-control-sm sales-source-quantity');
            quantity.type = 'number'; quantity.min = '1'; quantity.max = '2147483647'; quantity.step = '1';
            quantity.value = choice.quantity;
            quantity.setAttribute('aria-label', 'Số lượng từ nguồn');
            const remove = element('button', 'order-source-remove', '×');
            remove.type = 'button'; remove.setAttribute('aria-label', 'Bỏ nguồn khỏi phân bổ');
            remove.addEventListener('click', () => { entry.choices.splice(index, 1); render(row); });
            select.addEventListener('change', () => {
                choice.key = select.value;
                const source = options.find(s => key(s) === choice.key);
                const allocated = entry.choices.reduce((sum, c, i) => sum + (i === index ? 0 : Number(c.quantity) || 0), 0);
                if (source) choice.quantity = Math.max(1, Math.min(source.availableQuantity, Math.max(0, demand(row) - allocated)));
                render(row);
            });
            quantity.addEventListener('input', () => { choice.quantity = quantity.value; sync(row); });
            const sourceInput = element('input'); sourceInput.type = 'hidden'; sourceInput.dataset.sourceField = 'SupplySourceId';
            const transferInput = element('input'); transferInput.type = 'hidden'; transferInput.dataset.sourceField = 'TransferLineId';
            const source = options.find(s => key(s) === choice.key);
            const metadata = element('div', 'order-source-meta');
            if (source) {
                select.title = describe(source);
                metadata.append(element('span', 'order-source-kind', sourceKind(source)),
                    element('span', '', `Khả dụng: ${source.availableQuantity.toLocaleString('vi-VN')}`));
                if (source.supplyState === 'INCOMING') metadata.append(element('span', '', `Ngày đến: ${dateLabel(source.availableFrom)}`));
                if (source.warning) metadata.append(element('span', 'order-source-warning', source.warning));
            }
            wrapper.append(select, quantity, remove, metadata, sourceInput, transferInput);
            entry.list.append(wrapper);
        });
        sync(row);
    }

    function sync(row) {
        const entry = state.get(row);
        const lineIndex = rows().indexOf(row);
        let total = 0, message = '', used = new Set();
        Array.from(entry.list.children).forEach((wrapper, index) => {
            const choice = entry.choices[index];
            const option = catalog.find(s => key(s) === choice.key && s.productId === productId(row));
            const sourceId = choice.key.startsWith('S:') ? choice.key.slice(2) : '0';
            const transferId = choice.key.startsWith('T:') ? choice.key.slice(2) : '';
            wrapper.querySelector('[data-source-field="SupplySourceId"]').name = `Lines[${lineIndex}].Sources[${index}].SupplySourceId`;
            wrapper.querySelector('[data-source-field="SupplySourceId"]').value = sourceId;
            wrapper.querySelector('[data-source-field="TransferLineId"]').name = `Lines[${lineIndex}].Sources[${index}].TransferLineId`;
            wrapper.querySelector('[data-source-field="TransferLineId"]').value = transferId;
            wrapper.querySelector('.sales-source-quantity').name = `Lines[${lineIndex}].Sources[${index}].Quantity`;
            const qty = Number(choice.quantity);
            total += Number.isFinite(qty) ? qty : 0;
            if (!option?.canSelect) message = 'Nguồn không còn đủ điều kiện. Chọn lại nguồn.';
            else if (!Number.isInteger(qty) || qty <= 0 || qty > option.availableQuantity) message = `Nhập số nguyên từ 1 đến ${option.availableQuantity}.`;
            if (used.has(choice.key)) message = 'Nguồn bị chọn trùng.';
            used.add(choice.key);
        });
        if (!message && (replacement ? total > demand(row) : total !== demand(row)))
            message = total > demand(row) ? `Vượt ${total - demand(row)} sản phẩm.` : `Còn ${demand(row) - total} chưa phân bổ.`;
        entry.note.textContent = `Đã chọn ${total}/${demand(row)}${message ? ' · ' + message : ''}`;
        entry.note.className = `order-source-note${message ? ' is-invalid' : ''}`;
        entry.error = message;
        return !message;
    }

    async function load() {
        const current = ++version;
        request?.abort(); request = new AbortController(); ready = false;
        status.className = 'order-sources-status'; status.textContent = warehouse.value ? 'Đang tải nguồn…' : 'Chọn kho xuất để chọn nguồn.';
        rows().forEach(render);
        if (!warehouse.value) { catalog = []; rows().forEach(render); return; }
        try {
            const url = new URL(form.dataset.sourceUrl || '/SalesOrder/GetSourceOptions', location.origin);
            url.searchParams.set('warehouseId', warehouse.value);
            if (deadline?.value) url.searchParams.set('requestedDate', deadline.value);
            const response = await fetch(url, { signal: request.signal, headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error();
            const data = await response.json();
            if (current !== version) return;
            catalog = data; ready = true;
            status.textContent = '';
            rows().forEach(render);
        } catch (error) {
            if (error.name === 'AbortError' || current !== version) return;
            catalog = []; status.className = 'order-sources-status is-error'; status.textContent = 'Không tải được nguồn hàng. ';
            const retry = element('button', 'btn btn-sm btn-outline-secondary', 'Tải lại nguồn'); retry.type = 'button';
            retry.addEventListener('click', load); status.append(retry); rows().forEach(render);
        }
    }
    rows().forEach((row, index) => init(row, initial[index]?.sources || []));
    new MutationObserver(records => {
        if (!records.some(record => record.target === body)) return;
        rows().forEach(row => { init(row); sync(row); });
    }).observe(body, { childList: true });
    body.addEventListener('change', event => {
        if (!event.target.matches('.product-select')) return;
        const row = event.target.closest('tr'), entry = state.get(row);
        if (entry.product !== productId(row)) { entry.choices = []; entry.product = productId(row); }
        render(row);
    });
    body.addEventListener('input', event => { if (event.target.matches('.qty-input')) sync(event.target.closest('tr')); });
    warehouse.addEventListener('change', () => {
        rows().forEach(row => { state.get(row).choices = []; });
        load();
    });
    deadline?.addEventListener('change', load);
    form.addEventListener('submit', event => {
        const valid = rows().map(sync).every(Boolean);
        const hasQuantity = rows().some(row => state.get(row).choices.length > 0);
        if (!ready || !valid || !hasQuantity) {
            event.preventDefault(); event.stopImmediatePropagation();
            status.className = 'order-sources-status is-error';
            status.textContent = !ready ? 'Tải lại nguồn trước khi lưu.' : 'Kiểm tra phân bổ nguồn ở các dòng sản phẩm.';
            status.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
    }, true);
    load();
});
