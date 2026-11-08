(() => {
    function bindConfirm(root = document) {
        root.querySelectorAll('[data-confirm]:not(.inbound-remove-form):not([data-confirm-bound])').forEach(form => {
            form.dataset.confirmBound = 'true';
            form.addEventListener('submit', event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); });
        });
    }
    bindConfirm();
    document.querySelectorAll('[data-submit-once]').forEach(form => form.addEventListener('submit', event => {
        if (form.classList.contains('inbound-serial-form') || event.defaultPrevented || !form.reportValidity()) return;
        const button = form.querySelector('button[type="submit"], button:not([type])');
        if (button) setTimeout(() => { button.disabled = true; button.textContent = 'Đang xử lý…'; }, 0);
    }));

    const cancelDialog = document.querySelector('[data-cancel-dialog]');
    document.querySelector('[data-open-cancel]')?.addEventListener('click', () => {
        cancelDialog?.showModal();
        cancelDialog?.querySelector('[name="reason"]')?.focus();
    });
    document.querySelector('[data-close-cancel]')?.addEventListener('click', () => cancelDialog?.close());
    cancelDialog?.addEventListener('click', event => { if (event.target === cancelDialog) cancelDialog.close(); });

    const form = document.querySelector('.inbound-serial-form');
    if (!form) return;
    function showToast(text, kind = 'success') {
        const container = document.querySelector('.toast-container');
        if (!container || !window.bootstrap?.Toast) return;
        const toast = document.createElement('div');
        toast.className = 'toast align-items-center border-0 shadow-lg bg-white';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', kind === 'success' ? 'polite' : 'assertive');
        toast.setAttribute('aria-atomic', 'true');
        const row = document.createElement('div');
        row.className = `d-flex border-start border-${kind === 'success' ? 'success' : 'danger'} border-4 rounded-start`;
        const body = document.createElement('div');
        body.className = 'toast-body d-flex align-items-center gap-2 text-dark';
        const icon = document.createElement('i');
        icon.className = `bi bi-${kind === 'success' ? 'check-circle-fill text-success' : 'exclamation-triangle-fill text-danger'} fs-5 flex-shrink-0`;
        icon.setAttribute('aria-hidden', 'true');
        const label = document.createElement('div');
        label.textContent = text;
        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close me-2 m-auto';
        close.setAttribute('data-bs-dismiss', 'toast');
        close.setAttribute('aria-label', 'Đóng thông báo');
        body.append(icon, label);
        row.append(body, close);
        toast.append(row);
        toast.addEventListener('hidden.bs.toast', () => toast.remove());
        container.append(toast);
        bootstrap.Toast.getOrCreateInstance(toast, { autohide: true, delay: 3000 }).show();
    }
    const capture = window.EwmsSerialCapture.get(form.querySelector('[data-serial-capture]'));
    const serial = form.querySelector('[data-serial-input]');
    const message = form.querySelector('[data-capture-message]');
    const month = form.querySelector('[name="NewSerial.Month"]');
    const year = form.querySelector('[name="NewSerial.Year"]');
    const keep = form.querySelector('[data-keep-production]');

    async function refreshDetail(anchor) {
        const anchorTop = anchor.getBoundingClientRect().top;
        const detailResponse = await fetch(window.location.pathname + window.location.search, { credentials: 'same-origin', cache: 'no-store' });
        if (!detailResponse.ok) throw new Error('Không tải được chi tiết phiếu mới.');
        const updated = new DOMParser().parseFromString(await detailResponse.text(), 'text/html');
        for (const selector of ['.inbound-progress-section', '.inbound-serial-list']) {
            const current = document.querySelector(selector);
            const fresh = updated.querySelector(selector);
            if (current && fresh) current.replaceWith(fresh);
        }
        const nextLineSelect = updated.querySelector('select[name="NewSerial.LineId"]');
        const lineSelect = form.querySelector('select[name="NewSerial.LineId"]');
        if (nextLineSelect && lineSelect) {
            const currentLine = lineSelect.value;
            lineSelect.innerHTML = nextLineSelect.innerHTML;
            if ([...lineSelect.options].some(option => option.value === currentLine)) lineSelect.value = currentLine;
        }
        const nextVersion = updated.querySelector('input[name="NewSerial.Version"]')?.value;
        if (nextVersion) document.querySelectorAll('input[name="version"], input[name="Version"], input[name="NewSerial.Version"]').forEach(input => { input.value = nextVersion; });
        window.scrollBy(0, anchor.getBoundingClientRect().top - anchorTop);
    }

    async function postInline(inlineForm) {
        const response = await fetch(inlineForm.action, {
            method: 'POST', body: new FormData(inlineForm), credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
        });
        if (!response.headers.get('content-type')?.includes('application/json'))
            throw new Error('Phiên đăng nhập đã hết hạn hoặc yêu cầu bị từ chối. Vui lòng đăng nhập lại.');
        const result = await response.json();
        if (!response.ok || !result.ok) throw new Error(result.message || 'Không thể lưu thay đổi Serial.');
        return result;
    }

    document.addEventListener('submit', async event => {
        const inlineForm = event.target.closest('.inbound-edit-form, .inbound-remove-form');
        if (!inlineForm) return;
        event.preventDefault();
        if (!inlineForm.reportValidity()) return;
        if (inlineForm.classList.contains('inbound-remove-form') && !window.confirm(inlineForm.dataset.confirm)) return;
        const button = inlineForm.querySelector('button[type="submit"], button:not([type])');
        if (button) button.disabled = true;
        try {
            const result = await postInline(inlineForm);
            await refreshDetail(document.querySelector('.inbound-work-grid'));
            showToast(result.message);
        } catch (error) {
            showToast(error.message || 'Không thể lưu thay đổi Serial.', 'error');
        } finally {
            if (button?.isConnected) button.disabled = false;
        }
    });

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (!form.reportValidity()) return;
        const button = form.querySelector('button[type="submit"]');
        const originalLabel = button.textContent;
        const payload = new FormData(form);
        capture.setDisabled(true);
        button.disabled = true;
        button.textContent = 'Đang lưu Serial…';
        message.classList.remove('is-error', 'is-success');
        message.textContent = '';
        try {
            const response = await fetch(form.action, {
                method: 'POST', body: payload, credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
            });
            if (!response.headers.get('content-type')?.includes('application/json'))
                throw new Error('Phiên đăng nhập đã hết hạn hoặc yêu cầu bị từ chối. Vui lòng đăng nhập lại.');
            const result = await response.json();
            if (!response.ok || !result.ok) throw new Error(result.message || 'Không thể lưu Serial. Vui lòng thử lại.');
            capture.reset();
            form.querySelector('[name="NewSerial.Condition"]').value = 'GOOD';
            form.querySelector('[name="NewSerial.ConditionNote"]').value = '';
            if (!keep.checked) { month.value = ''; year.value = ''; }
            try {
                await refreshDetail(form);
            } catch {
                message.textContent = '';
                showToast('Đã lưu Serial, nhưng chưa cập nhật được danh sách. Tải lại trang để xem.', 'success');
                serial.focus({ preventScroll: true });
                return;
            }
            message.textContent = '';
            showToast('Đã thêm Serial vào phiếu nháp.', 'success');
            serial.focus({ preventScroll: true });
        } catch (error) {
            message.textContent = error.message || 'Không lưu được Serial. Vui lòng thử lại.';
            message.classList.add('is-error');
        } finally {
            capture.setDisabled(false);
            button.disabled = false;
            button.textContent = originalLabel;
            serial.focus({ preventScroll: true });
        }
    });
    form.addEventListener('submit', event => {
        if (!form.reportValidity()) return;
        if (keep.checked) sessionStorage.setItem(`inbound-production-${form.action}`, JSON.stringify({ month: month.value, year: year.value }));
        else sessionStorage.removeItem(`inbound-production-${form.action}`);
    });
    try {
        const saved = JSON.parse(sessionStorage.getItem(`inbound-production-${form.action}`));
        if (saved) { month.value = saved.month; year.value = saved.year; keep.checked = true; }
    } catch { sessionStorage.removeItem(`inbound-production-${form.action}`); }
    serial.focus();
})();
