(() => {
    'use strict';

    function init() {
        const workbench = document.querySelector('[data-outbound-workbench]');
        const maxBatch = 1000;
        let busy = false;
        let selectedProduct = 'ALL';
        const savedDisabled = new WeakMap();
        const scanForm = document.getElementById('scanSerialForm');
        const captureRoot = scanForm?.querySelector('[data-serial-capture]');
        const capture = () => window.EwmsSerialCapture?.get(captureRoot);
        if (captureRoot) {
            const actions = captureRoot.querySelector('.serial-capture-actions');
            const camera = captureRoot.querySelector('[data-camera-box]');
            if (actions && camera) {
                const tools = document.createElement('details');
                tools.className = 'outbound-capture-tools';
                const summary = document.createElement('summary');
                summary.textContent = 'Camera hoặc ảnh tem';
                const body = document.createElement('div');
                const help = captureRoot.querySelector('p.muted');
                const imageHint = actions.nextElementSibling;
                tools.append(summary, body);
                if (help) body.append(help);
                body.append(actions);
                if (imageHint?.matches('small')) body.append(imageHint);
                body.append(camera);
                const sourceHint = captureRoot.querySelector('[data-capture-source]');
                if (sourceHint) sourceHint.after(tools); else captureRoot.append(tools);
            }
        }
        const checks = () => [...(workbench?.querySelectorAll('.outbound-pick-check') || [])];
        const counts = () => {
            const result = { products: new Map(), sources: new Map(), total: 0 };
            checks().filter(input => input.checked).forEach(input => {
                result.products.set(input.dataset.productId, (result.products.get(input.dataset.productId) || 0) + 1);
                result.sources.set(input.dataset.sourceId, (result.sources.get(input.dataset.sourceId) || 0) + 1);
                result.total++;
            });
            return result;
        };
        const hasRoom = (input, tally) => tally.total < maxBatch
            && (tally.products.get(input.dataset.productId) || 0) < Number(input.dataset.productLimit)
            && (tally.sources.get(input.dataset.sourceId) || 0) < Number(input.dataset.sourceLimit);

        function notice(message, kind = 'success') {
            const target = document.getElementById('outbound-work-message');
            if (!target) return;
            target.textContent = message;
            target.className = 'outbound-notice outbound-notice-' + kind;
            target.setAttribute('role', kind === 'danger' ? 'alert' : 'status');
            target.hidden = !message;
        }

        function updateSelection() {
            if (!workbench) return;
            const tally = counts();
            checks().forEach(input => {
                input.disabled = busy || input.dataset.selectable !== 'true' || (!input.checked && !hasRoom(input, tally));
                input.closest('tr')?.classList.toggle('outbound-checked-row', input.checked);
            });
            const visible = checks().filter(input => !input.closest('tr').hidden && input.dataset.selectable === 'true');
            const checked = visible.filter(input => input.checked).length;
            const available = visible.some(input => !input.checked && hasRoom(input, tally));
            const selectAll = document.getElementById('outbound-select-all');
            if (selectAll) {
                selectAll.disabled = busy || (!checked && !available);
                selectAll.checked = checked > 0 && !available;
                selectAll.indeterminate = checked > 0 && available;
            }
            const count = document.getElementById('outbound-checked-count');
            if (count) count.textContent = tally.total ? 'Đã tích ' + tally.total + ' serial' : 'Chưa tích serial';
            const total = workbench.querySelector('[data-checked-total]');
            if (total) total.textContent = '(' + tally.total + ')';
            const add = document.getElementById('outbound-add-checked');
            if (add) add.disabled = busy || tally.total === 0;
            const canAdd = workbench.dataset.canAdd === 'true';
            const scanButton = workbench.querySelector('[data-outbound-scan-submit]');
            if (scanButton) scanButton.disabled = busy || !canAdd;
            const scanLimit = workbench.querySelector('[data-outbound-scan-limit]');
            if (scanLimit) scanLimit.hidden = canAdd;
            capture()?.setDisabled(busy || !canAdd);
        }

        function setBusy(value) {
            busy = value;
            document.querySelectorAll('form[data-outbound-submit-once] button[type="submit"], form[data-outbound-ajax] button[type="submit"], [data-outbound-refresh], [data-transfer-receive-pick]').forEach(button => {
                if (value) {
                    if (!savedDisabled.has(button)) savedDisabled.set(button, button.disabled);
                    button.disabled = true;
                } else if (savedDisabled.has(button)) {
                    button.disabled = savedDisabled.get(button);
                    savedDisabled.delete(button);
                }
            });
            workbench?.setAttribute('aria-busy', String(value));
            updateSelection();
        }

        function filterSuggestions() {
            if (!workbench) return;
            const search = document.getElementById('outbound-serial-search');
            const term = (search?.value || '').trim().toLocaleLowerCase('vi');
            let shown = 0;
            const rows = [...workbench.querySelectorAll('.suggested-serial-row')];
            rows.forEach(row => {
                const visible = (selectedProduct === 'ALL' || row.dataset.productId === selectedProduct)
                    && (row.dataset.suggestionSearch || '').toLocaleLowerCase('vi').includes(term);
                row.hidden = !visible;
                if (visible) shown++;
            });
            workbench.querySelectorAll('[data-product-filter]').forEach(button => {
                const active = button.dataset.productFilter === selectedProduct;
                button.classList.toggle('active', active);
                button.setAttribute('aria-pressed', String(active));
            });
            document.getElementById('outbound-suggestions-empty')?.classList.toggle('d-none', shown > 0 || rows.length === 0);
            updateSelection();
        }

        function followRedirect(response) {
            if (!response.redirected) return false;
            const target = new URL(response.url, location.href);
            if (target.origin === location.origin) location.assign(target.href);
            throw new Error('Phiên làm việc đã thay đổi. Hãy mở lại phiếu xuất.');
        }

        async function refreshWorkbench(preserveChecked = false) {
            const response = await fetch(workbench.dataset.refreshUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, cache: 'no-store' });
            followRedirect(response);
            if (response.status === 409) {
                const result = await response.json();
                if (result.redirectUrl) {
                    const target = new URL(result.redirectUrl, location.href);
                    if (target.origin === location.origin) location.assign(target.href);
                }
                throw new Error('Phiếu đã hoàn tất hoặc bị hủy.');
            }
            if (!response.ok) throw new Error('Chưa cập nhật được danh sách. Bấm Cập nhật để lấy số liệu mới.');
            const html = new DOMParser().parseFromString(await response.text(), 'text/html');
            const incoming = html.querySelector('[data-outbound-workbench]');
            if (!incoming) throw new Error('Không nhận được nội dung phiếu xuất. Hãy kiểm tra phiên đăng nhập.');
            const search = document.getElementById('outbound-serial-search')?.value || '';
            const pending = new Set(preserveChecked ? checks().filter(input => input.checked).map(input => input.value) : []);
            const openSources = new Set([...workbench.querySelectorAll('[data-source-details][open]')].map(details => details.dataset.sourceDetails));
            const scroll = new Map([...workbench.querySelectorAll('[data-outbound-scroll]')].map(element => [element.dataset.outboundScroll, [element.scrollTop, element.scrollLeft]]));
            const position = [window.scrollX, window.scrollY];
            // Render all changing blocks with the same MVC view; the shared scanner stays mounted.
            workbench.querySelectorAll('[data-outbound-fragment]').forEach(current => {
                const replacement = incoming.querySelector('[data-outbound-fragment="' + current.dataset.outboundFragment + '"]');
                if (replacement) current.replaceWith(replacement);
            });
            workbench.dataset.version = incoming.dataset.version;
            workbench.dataset.canAdd = incoming.dataset.canAdd;
            document.querySelectorAll('input[name="version"]').forEach(input => { input.value = incoming.dataset.version; });
            const searchInput = document.getElementById('outbound-serial-search');
            if (searchInput) searchInput.value = search;
            if (selectedProduct !== 'ALL' && ![...workbench.querySelectorAll('[data-product-filter]')].some(button => button.dataset.productFilter === selectedProduct))
                selectedProduct = 'ALL';
            workbench.querySelectorAll('[data-source-details]').forEach(details => { details.open = openSources.has(details.dataset.sourceDetails); });
            const tally = { products: new Map(), sources: new Map(), total: 0 };
            checks().forEach(input => {
                if (!pending.has(input.value) || input.dataset.selectable !== 'true' || !hasRoom(input, tally)) return;
                input.checked = true;
                tally.products.set(input.dataset.productId, (tally.products.get(input.dataset.productId) || 0) + 1);
                tally.sources.set(input.dataset.sourceId, (tally.sources.get(input.dataset.sourceId) || 0) + 1);
                tally.total++;
            });
            filterSuggestions();
            workbench.querySelectorAll('[data-outbound-scroll]').forEach(element => {
                const saved = scroll.get(element.dataset.outboundScroll);
                if (saved) { element.scrollTop = saved[0]; element.scrollLeft = saved[1]; }
            });
            window.scrollTo({ left: position[0], top: position[1], behavior: 'instant' });
            if (busy) setBusy(true);
        }

        document.addEventListener('submit', async event => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) return;
            if (form.matches('[data-outbound-ajax]') && workbench) {
                event.preventDefault();
                if (busy) return;
                if (form.hasAttribute('data-outbound-batch') && !counts().total) return;
                const wasScan = form === scanForm;
                const restoreFocus = wasScan && scanForm.contains(document.activeElement);
                // Capture checked values before locking the controls.
                const data = new FormData(form);
                let posted = false;
                setBusy(true);
                try {
                    const response = await fetch(form.action, { method: 'POST', body: data, headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' } });
                    followRedirect(response);
                    if (!response.headers.get('content-type')?.includes('application/json'))
                        throw new Error('Yêu cầu không hợp lệ hoặc phiên đăng nhập đã hết. Hãy cập nhật lại phiếu.');
                    const result = await response.json();
                    if (!response.ok) throw new Error(result.message || 'Không thể cập nhật phiếu xuất.');
                    posted = result.success === true;
                if (posted && wasScan) {
                    capture()?.reset();
                    if (workbench.dataset.workMode === 'transfer-receive') {
                        scanForm.querySelector('[name="receivedCondition"]').value = '';
                        scanForm.querySelector('[name="conditionNote"]').value = '';
                    }
                }
                    await refreshWorkbench(!posted);
                    notice(result.message, posted ? 'success' : 'danger');
                } catch (error) {
                    if (posted) checks().forEach(input => { input.checked = false; });
                    notice((posted ? 'Lựa chọn đã được lưu. ' : '') + (error.message || 'Không thể kết nối. Hãy cập nhật số liệu trước khi thử lại.'), 'danger');
                } finally {
                    setBusy(false);
                    if (restoreFocus && workbench.dataset.canAdd === 'true')
                        scanForm.querySelector('[data-serial-input]')?.focus({ preventScroll: true });
                }
                return;
            }
            if (!form.matches('[data-outbound-submit-once]')) return;
            if (busy) { event.preventDefault(); return; }
            if (form.dataset.outboundConfirm && !window.confirm(form.dataset.outboundConfirm)) { event.preventDefault(); return; }
            setBusy(true);
            form.setAttribute('aria-busy', 'true');
        });

        workbench?.addEventListener('change', event => {
            const input = event.target;
            if (input.id === 'outbound-select-all') {
                const inputs = checks().filter(check => !check.closest('tr').hidden && check.dataset.selectable === 'true');
                if (!input.checked) inputs.forEach(check => { check.checked = false; });
                else {
                    const tally = counts();
                    inputs.forEach(check => {
                        if (check.checked || !hasRoom(check, tally)) return;
                        check.checked = true;
                        tally.products.set(check.dataset.productId, (tally.products.get(check.dataset.productId) || 0) + 1);
                        tally.sources.set(check.dataset.sourceId, (tally.sources.get(check.dataset.sourceId) || 0) + 1);
                        tally.total++;
                    });
                    if (tally.total === maxBatch) notice('Đã tích tối đa 1.000 serial cho lượt này.', 'warning');
                }
                updateSelection();
            } else if (input.matches('.outbound-pick-check')) updateSelection();
        });
        workbench?.addEventListener('input', event => {
            if (event.target.id === 'outbound-serial-search') filterSuggestions();
        });
        workbench?.addEventListener('click', async event => {
            const receive = event.target.closest('[data-transfer-receive-pick]');
            if (receive) {
                if (busy || !scanForm) return;
                capture()?.reset();
                scanForm.querySelector('[data-serial-input]').value = receive.dataset.transferReceivePick;
                scanForm.querySelector('[data-capture-method]').value = 'QUICK_SELECT';
                const condition = scanForm.querySelector('[name="receivedCondition"]');
                const panel = scanForm.closest('section');
                const bounds = panel.getBoundingClientRect();
                if (bounds.top < 0 || bounds.bottom > window.innerHeight)
                    panel.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
                condition?.focus({ preventScroll: true });
                notice('Đã chọn serial ' + receive.dataset.transferReceivePick + '. Kiểm tra tình trạng thực nhận rồi ghi nhận.');
                return;
            }
            const filter = event.target.closest('[data-product-filter]');
            if (filter) { selectedProduct = filter.dataset.productFilter; filterSuggestions(); return; }
            if (!event.target.closest('[data-outbound-refresh]') || busy) return;
            setBusy(true);
            try { await refreshWorkbench(true); notice('Đã cập nhật hạn mức và danh sách serial.'); }
            catch (error) { notice(error.message, 'danger'); }
            finally { setBusy(false); }
        });
        filterSuggestions();

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
        // A cached page must refresh its version and request token before another mutation.
        window.addEventListener('pageshow', event => { if (event.persisted) window.location.reload(); });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
