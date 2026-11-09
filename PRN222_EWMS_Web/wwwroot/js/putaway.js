(() => {
    const root = document.querySelector('[data-putaway]');
    const lookupForm = root?.querySelector('[data-putaway-lookup]');
    if (!lookupForm) return;
    const queue = root.querySelector('[data-putaway-queue]');
    const detail = root.querySelector('[data-putaway-detail]');
    const placeholder = detail.innerHTML;
    const captureRoot = lookupForm.querySelector('[data-serial-capture]');
    const capture = window.EwmsSerialCapture.get(captureRoot);
    const serialInput = captureRoot.querySelector('[data-serial-input]');
    const success = root.querySelector('[data-putaway-success]');
    const error = root.querySelector('[data-putaway-error]');
    const dialog = root.querySelector('[data-putaway-confirm]');
    const confirmForm = dialog.querySelector('[data-confirm-form]');
    const confirmError = dialog.querySelector('[data-confirm-error]');
    let selected = null, saving = false, detailLoading = false, queueLoading = false, needsRefresh = false, resetting = false;
    let detailRequest = null, queueRequest = null, detailGeneration = 0, queueGeneration = 0;
    const busy = () => saving || detailLoading || queueLoading;
    const show = (element, text) => { element.textContent = text || ''; element.hidden = !text; };
    function notifySuccess(text) {
        show(success, text);
        const container = document.querySelector('.toast-container');
        if (!container || !window.bootstrap?.Toast) return;
        const toast = document.createElement('div');
        toast.className = 'toast align-items-center border-0 shadow-lg bg-white';
        toast.setAttribute('role', 'status'); toast.setAttribute('aria-live', 'polite');
        const row = document.createElement('div'); row.className = 'd-flex border-start border-success border-4 rounded-start';
        const body = document.createElement('div'); body.className = 'toast-body text-dark'; body.textContent = text;
        const close = document.createElement('button'); close.type = 'button'; close.className = 'btn-close me-2 m-auto';
        close.dataset.bsDismiss = 'toast'; close.setAttribute('aria-label', 'Đóng thông báo');
        row.append(body, close); toast.append(row); container.append(toast);
        toast.addEventListener('hidden.bs.toast', () => toast.remove());
        bootstrap.Toast.getOrCreateInstance(toast, { autohide: true, delay: 4000 }).show();
    }
    const normalize = value => (value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[đĐ]/g, 'd').toLowerCase()
        .replace(/\bzone(?=[a-z0-9])/g, 'zone ').replace(/\bosan(?=\s*\d)/g, 'o san ')
        .replace(/([a-z])(\d)/g, '$1 $2').replace(/[^a-z0-9]+/g, ' ').trim().replace(/\b0+(?=\d)/g, '').replace(/\s+/g, ' ');
    const dimensions = ['zone', 'row', 'rack', 'leaf'];
    function controls() {
        capture.setDisabled(busy() || dialog.open);
        lookupForm.querySelector('button[type="submit"]').disabled = busy() || dialog.open;
        queue.querySelectorAll('input,select,button').forEach(element => { element.disabled = busy() || dialog.open; });
        detail.querySelectorAll('[data-refresh-locations],[data-clear-location-filter],[data-location-query]').forEach(element => { element.disabled = busy() || dialog.open; });
        detail.querySelectorAll('[data-location-group]').forEach(element => { element.disabled = busy() || needsRefresh || dialog.open; });
        detail.querySelectorAll('[data-location-filter]').forEach(element => { element.disabled = busy() || dialog.open || element.options.length <= 1; });
        confirmForm.querySelector('[data-confirm-submit]').disabled = saving || needsRefresh;
        confirmForm.querySelector('[data-confirm-submit]').hidden = needsRefresh;
        dialog.querySelector('[data-confirm-refresh]').hidden = !needsRefresh;
        dialog.querySelector('[data-confirm-refresh]').disabled = saving;
        dialog.querySelector('[data-confirm-cancel]').disabled = saving;
        updateSelection();
    }
    function clearDetail(clearInput = false) {
        detailGeneration++; detailRequest?.abort(); selected = null; needsRefresh = false;
        detail.replaceChildren(); detail.innerHTML = placeholder;
        if (dialog.open) dialog.close();
        show(confirmError, '');
        if (clearInput) { resetting = true; capture.reset(); resetting = false; }
        queue.querySelectorAll('[data-pending-serial]').forEach(item => item.classList.remove('is-selected'));
    }
    function parameters() {
        const form = queue.querySelector('[data-putaway-filter]');
        // The queue is disabled while confirming; read values explicitly so RETURN/receipt filters survive refresh.
        const params = new URLSearchParams();
        ['Source', 'ReceiptId', 'Search', 'Condition'].forEach(name => { const value = form.elements.namedItem(name)?.value; if (value) params.set(name, value); });
        params.set('Page', queue.querySelector('[data-queue-state]')?.dataset.page || '1');
        return params;
    }
    async function readHtml(url, signal) {
        const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', signal, headers: { Accept: 'text/html' } });
        if (response.redirected) throw new Error('Phiên đăng nhập đã thay đổi. Hãy tải lại trang hoặc đăng nhập lại.');
        const html = await response.text();
        if (!response.ok) throw new Error(response.headers.get('content-type')?.includes('text/html')
            ? 'Yêu cầu bị từ chối hoặc dữ liệu chưa khả dụng. Hãy tải lại trang.' : html || 'Không tải được dữ liệu.');
        if (!response.headers.get('content-type')?.includes('text/html')) throw new Error('Không tải được giao diện Putaway.');
        return html;
    }
    async function loadQueue(params, clear = false) {
        if (clear) { clearDetail(true); show(success, ''); }
        queueRequest?.abort(); const current = ++queueGeneration;
        queueRequest = new AbortController(); queueLoading = true; queue.setAttribute('aria-busy', 'true'); controls();
        try {
            const url = new URL(root.dataset.pendingUrl, location.origin); url.search = params.toString();
            const html = await readHtml(url, queueRequest.signal);
            if (current !== queueGeneration) return;
            queue.innerHTML = html;
            const address = new URL(location.href); address.search = params.toString(); history.replaceState(null, '', address);
            highlightSerial();
        } catch (exception) {
            if (exception.name === 'AbortError') return;
            queue.querySelector('[data-putaway-filter]')?.reset();
            show(error, exception.message); throw exception;
        } finally {
            if (current === queueGeneration) { queueLoading = false; queue.removeAttribute('aria-busy'); controls(); }
        }
    }
    function highlightSerial() {
        queue.querySelectorAll('[data-pending-serial]').forEach(item => item.classList.toggle('is-selected', item.dataset.pendingSerial === selected?.serial));
    }
    async function lookup(value, preserve = false) {
        if (saving || !value?.trim()) return;
        const savedFilters = preserve ? Object.fromEntries([...detail.querySelectorAll('[data-location-filter]')].map(select => [select.dataset.locationFilter, select.value])) : {};
        const savedQuery = preserve ? detail.querySelector('[data-location-query]')?.value || '' : '';
        const savedTarget = preserve ? detail.querySelector('[name="SelectedLocationId"]:checked')?.value : null;
        clearDetail(); const current = ++detailGeneration;
        detailRequest = new AbortController(); detailLoading = true; show(error, ''); show(success, '');
        detail.innerHTML = '<div class="workspace-card putaway-placeholder" role="status">Đang kiểm tra Serial và vị trí phù hợp…</div>';
        controls(); capture.stop();
        try {
            const url = new URL(root.dataset.lookupUrl, location.origin); url.searchParams.set('serial', value.trim());
            const html = await readHtml(url, detailRequest.signal);
            if (current !== detailGeneration) return;
            detail.innerHTML = html;
            const state = detail.querySelector('[data-putaway-serial]');
            if (!state) throw new Error('Không đọc được chi tiết Serial. Hãy tải lại trang.');
            selected = { ...state.dataset }; serialInput.value = selected.serial; needsRefresh = false;
            const queueState = queue.querySelector('[data-queue-state]');
            if (selected.receiptId && (queueState.dataset.source !== selected.source || (queueState.dataset.receiptId && queueState.dataset.receiptId !== selected.receiptId))) {
                const params = new URLSearchParams({ Source: selected.source, ReceiptId: selected.receiptId, Page: '1' });
                await loadQueue(params);
                if (current !== detailGeneration) return;
            }
            detail.querySelectorAll('[data-location-filter]').forEach(select => { select.dataset.restoreValue = savedFilters[select.dataset.locationFilter] || ''; });
            buildFilters(true);
            const search = detail.querySelector('[data-location-query]'); if (search) search.value = savedQuery;
            const options = [...detail.querySelectorAll('[data-location-option]')];
            const preferred = options.find(option => option.dataset.id === savedTarget) || options[0];
            if (preferred) preferred.querySelector('input[type="radio"]').checked = true;
            applyLocationFilter(); highlightSerial();
            if (innerWidth <= 1000) detail.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } catch (exception) {
            if (exception.name !== 'AbortError' && current === detailGeneration) { selected = null; detail.innerHTML = placeholder; show(error, exception.message); }
        } finally {
            if (current === detailGeneration) { detailLoading = false; controls(); }
        }
    }
    function dimensionValue(option, key) { return key === 'leaf' ? option.dataset.id : option.dataset[key + 'Id'] || ''; }
    function dimensionLabel(option, key) { return option.dataset[key] || ''; }
    function buildFilters(restore = false) {
        const options = [...detail.querySelectorAll('[data-location-option]')];
        dimensions.forEach((key, index) => {
            const select = detail.querySelector(`[data-location-filter="${key}"]`); if (!select) return;
            const previous = restore ? select.dataset.restoreValue || '' : select.value;
            const choices = new Map();
            options.filter(option => dimensions.slice(0, index).every(parent => {
                const value = detail.querySelector(`[data-location-filter="${parent}"]`).value;
                return !value || dimensionValue(option, parent) === value;
            })).forEach(option => {
                const id = dimensionValue(option, key);
                const context = dimensions.slice(0, index).map(parent => dimensionLabel(option, parent)).filter(Boolean).join(' / ');
                if (id) choices.set(id, { label: dimensionLabel(option, key), context });
            });
            select.replaceChildren(new Option('Tất cả', ''));
            [...choices.entries()].sort((a, b) => a[1].label.localeCompare(b[1].label, 'vi', { numeric: true }) || a[1].context.localeCompare(b[1].context, 'vi', { numeric: true }))
                .forEach(([id, entry]) => {
                    const duplicate = [...choices.values()].filter(other => normalize(other.label) === normalize(entry.label)).length > 1;
                    select.add(new Option(entry.label + (duplicate && entry.context ? ` — ${entry.context}` : ''), id));
                });
            if (choices.has(previous)) select.value = previous;
            select.disabled = busy() || choices.size === 0;
        });
    }
    function matchesQuery(option, query) {
        const tokens = normalize(query).split(' ').filter(Boolean);
        const words = new Set(normalize(`${option.dataset.path} zone ${option.dataset.zone}`).split(' '));
        const labels = { zone: 'zone', day: 'row', ke: 'rack', tang: 'leaf' };
        for (let index = 0; index < tokens.length; index++) {
            const token = tokens[index];
            if (labels[token] && tokens[index + 1]) {
                const label = normalize(dimensionLabel(option, labels[token]));
                const target = token === 'zone' ? label.replace(/^zone\s+/, '') : label; const next = tokens[++index];
                if (target !== (token === 'zone' ? next : `${token} ${next}`)) return false;
            } else if (token === 'o' && tokens[index + 1] === 'san' && tokens[index + 2]) {
                if (normalize(option.dataset.leaf) !== `o san ${tokens[index + 2]}`) return false;
                index += 2;
            } else if (!words.has(token)) return false;
        }
        return true;
    }
    function applyLocationFilter() {
        const options = [...detail.querySelectorAll('[data-location-option]')];
        const query = detail.querySelector('[data-location-query]')?.value || '';
        let count = 0;
        options.forEach(option => {
            option.hidden = !dimensions.every(key => { const value = detail.querySelector(`[data-location-filter="${key}"]`).value; return !value || dimensionValue(option, key) === value; }) || !matchesQuery(option, query);
            if (!option.hidden) count++;
            else option.querySelector('input').checked = false;
        });
        detail.querySelectorAll('[data-location-group]').forEach(group => { group.hidden = ![...group.querySelectorAll('[data-location-option]')].some(option => !option.hidden); });
        const counter = detail.querySelector('[data-location-count]'); if (counter) counter.textContent = `${count} / ${options.length} vị trí hợp lệ`;
        const empty = detail.querySelector('[data-no-location-match]'); if (empty) empty.hidden = count > 0;
        updateSelection();
    }
    function chosenLocation() {
        const option = detail.querySelector('[name="SelectedLocationId"]:checked')?.closest('[data-location-option]');
        return option && !option.hidden ? option : null;
    }
    function updateSelection() {
        const chosen = chosenLocation();
        detail.querySelectorAll('[data-location-option]').forEach(option => option.classList.toggle('is-selected', option === chosen));
        const label = detail.querySelector('[data-selected-location]'); if (label) label.textContent = chosen?.dataset.path || 'Chưa chọn vị trí';
        const button = detail.querySelector('[data-open-putaway-confirm]'); if (button) button.disabled = !chosen || !selected || busy() || needsRefresh || dialog.open;
    }
    lookupForm.addEventListener('submit', event => { event.preventDefault(); if (!busy()) void lookup(serialInput.value); });
    captureRoot.addEventListener('serialcapture:changed', () => {
        if (resetting || busy()) return;
        if (selected && serialInput.value.trim() !== selected.serial) { clearDetail(); show(success, ''); controls(); }
    });
    queue.addEventListener('submit', event => {
        if (!event.target.matches('[data-putaway-filter]')) return;
        event.preventDefault(); if (busy() || dialog.open) return;
        const params = new URLSearchParams(new FormData(event.target)); params.set('Page', '1'); show(error, '');
        void loadQueue(params, true).catch(() => {});
    });
    queue.addEventListener('change', event => {
        if (event.target.matches('select') && !busy() && !dialog.open) event.target.form.requestSubmit();
    });
    queue.addEventListener('click', event => {
        const source = event.target.closest('button[data-source]');
        const item = event.target.closest('[data-select-serial]');
        const page = event.target.closest('[data-putaway-page]');
        if (page) event.preventDefault();
        if (busy() || dialog.open) return;
        if (source) {
            const params = new URLSearchParams({ Source: source.dataset.source, Page: '1' }); show(error, '');
            void loadQueue(params, true).catch(() => {});
        } else if (item) {
            resetting = true; capture.reset(); resetting = false; serialInput.value = item.dataset.selectSerial; void lookup(serialInput.value);
        } else if (page) void loadQueue(new URL(page.href).searchParams, true).catch(() => {});
    });
    detail.addEventListener('input', event => { if (event.target.matches('[data-location-query]')) applyLocationFilter(); });
    detail.addEventListener('change', event => {
        if (event.target.matches('[data-location-filter]')) {
            const index = dimensions.indexOf(event.target.dataset.locationFilter);
            dimensions.slice(index + 1).forEach(key => { detail.querySelector(`[data-location-filter="${key}"]`).value = ''; });
            buildFilters(); applyLocationFilter();
        } else if (event.target.matches('[name="SelectedLocationId"]')) updateSelection();
    });
    detail.addEventListener('click', event => {
        if (busy()) return;
        if (event.target.closest('[data-refresh-locations]') && selected) { void lookup(selected.serial, true); return; }
        if (event.target.closest('[data-clear-location-filter]')) {
            detail.querySelectorAll('[data-location-filter]').forEach(select => { select.value = ''; });
            detail.querySelector('[data-location-query]').value = ''; buildFilters(); applyLocationFilter();
        }
        if (event.target.closest('[data-open-putaway-confirm]') && selected && !needsRefresh) {
            const option = chosenLocation(); if (!option) return;
            dialog.querySelector('[data-confirm-serial]').textContent = selected.serial;
            dialog.querySelector('[data-confirm-product]').textContent = selected.product;
            dialog.querySelector('[data-confirm-path]').textContent = option.dataset.path;
            dialog.querySelector('[data-confirm-condition]').textContent = selected.condition === 'DAMAGED' ? 'Hàng hỏng' : 'Hàng tốt';
            dialog.querySelector('[data-confirm-quarantine]').hidden = selected.quarantined !== 'true';
            confirmForm.elements.SerialItemId.value = selected.id;
            confirmForm.elements.TargetLocationId.value = option.dataset.id;
            confirmForm.elements.RowVersion.value = selected.version;
            show(confirmError, ''); dialog.showModal(); controls();
        }
    });
    dialog.addEventListener('cancel', event => { if (saving) event.preventDefault(); });
    dialog.addEventListener('close', () => controls());
    dialog.querySelector('[data-confirm-cancel]').addEventListener('click', () => { if (!saving) dialog.close(); });
    dialog.querySelector('[data-confirm-refresh]').addEventListener('click', () => { const value = selected?.serial; dialog.close(); if (value) void lookup(value, true); });
    confirmForm.addEventListener('submit', async event => {
        event.preventDefault(); if (!selected || saving || needsRefresh) return;
        const payload = new FormData(confirmForm); saving = true; show(confirmError, ''); show(error, ''); controls();
        try {
            const response = await fetch(confirmForm.action, { method: 'POST', body: payload, credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Yêu cầu chưa được xác nhận. Hãy tra cứu lại Serial để kiểm tra trạng thái.');
            const result = await response.json();
            if (!response.ok || !result.ok) throw new Error(result.message || 'Không xác nhận được Putaway.');
            dialog.close(); clearDetail(true); notifySuccess(result.message);
            try { await loadQueue(parameters()); }
            catch { show(error, 'Đã lưu Putaway, nhưng chưa tải lại được danh sách. Tải lại trang để xem dữ liệu mới.'); }
        } catch (exception) {
            needsRefresh = true; show(confirmError, exception.message); show(error, exception.message);
        } finally { saving = false; controls(); if (!dialog.open) serialInput.focus({ preventScroll: true }); }
    });
    window.addEventListener('pagehide', () => { detailGeneration++; queueGeneration++; detailRequest?.abort(); queueRequest?.abort(); capture.stop(); });
    controls(); serialInput.focus({ preventScroll: true });
})();
