document.addEventListener('submit', event => {
    const form = event.target.closest('[data-confirm]');
    if (form && !confirm(form.dataset.confirm)) event.preventDefault();
});

const scanForm = document.querySelector('[data-return-scan]');
if (scanForm) {
    const input = scanForm.querySelector('[data-serial-input]');
    const preview = scanForm.querySelector('[data-return-preview]');
    const error = scanForm.querySelector('[data-preview-error]');
    const addButton = scanForm.querySelector('[data-add-serial]');
    const condition = scanForm.querySelector('[name="condition"]');
    const note = scanForm.querySelector('[name="note"]');
    const noteError = scanForm.querySelector('[data-note-error]');
    const saveError = scanForm.querySelector('[data-save-error]');
    const saveSuccess = scanForm.querySelector('[data-save-success]');
    let timer;
    let request;
    let checkedSerial = '';

    function reset() {
        checkedSerial = '';
        preview.hidden = true;
        error.hidden = true;
        addButton.disabled = true;
        if (request) request.abort();
    }

    async function lookup() {
        const serial = input.value.trim();
        reset();
        if (!serial) return;
        request = new AbortController();
        try {
            const url = new URL(scanForm.dataset.previewUrl, location.href);
            url.searchParams.set('serial', serial);
            const response = await fetch(url, { signal: request.signal, headers: { Accept: 'application/json' } });
            const result = await response.json();
            if (input.value.trim() !== serial) return;
            if (!response.ok) throw new Error(result.message || 'Không tra cứu được Serial.');
            const data = result.data;
            scanForm.querySelector('[data-preview-product]').textContent = data.product;
            scanForm.querySelector('[data-preview-manufactured]').textContent = data.manufacturingMonth && data.manufacturingYear
                ? `${String(data.manufacturingMonth).padStart(2, '0')}/${data.manufacturingYear}` : 'Chưa có thông tin';
            scanForm.querySelector('[data-preview-condition]').textContent = data.soldCondition;
            scanForm.querySelector('[data-preview-note]').textContent = data.previousNote || 'Không có ghi chú';
            checkedSerial = serial;
            preview.hidden = false;
            addButton.disabled = false;
        } catch (e) {
            if (e.name === 'AbortError') return;
            error.textContent = e.message;
            error.hidden = false;
        }
    }

    function schedule() {
        clearTimeout(timer);
        reset();
        timer = setTimeout(lookup, 350);
    }
    input.addEventListener('input', schedule);
    scanForm.addEventListener('serialcapture:changed', schedule);
    function clearNoteError() {
        noteError.hidden = true;
        note.removeAttribute('aria-invalid');
    }
    function validateNote() {
        if (condition.value !== 'DAMAGED' || note.value.trim()) {
            clearNoteError();
            return true;
        }
        noteError.textContent = 'Vui lòng ghi rõ tình trạng hàng hỏng trước khi thêm Serial.';
        noteError.hidden = false;
        note.setAttribute('aria-invalid', 'true');
        note.focus();
        return false;
    }
    note.addEventListener('input', clearNoteError);
    condition.addEventListener('change', clearNoteError);
    // Click is handled as well because native form validation may prevent submit from firing.
    addButton.addEventListener('click', event => {
        if (!validateNote()) event.preventDefault();
    });
    input.addEventListener('keydown', event => {
        if (event.key === 'Enter') { event.preventDefault(); clearTimeout(timer); lookup(); }
    });
    scanForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (!validateNote()) { event.preventDefault(); return; }
        if (checkedSerial !== input.value.trim()) { clearTimeout(timer); lookup(); return; }
        saveError.hidden = true;
        saveSuccess.hidden = true;
        addButton.disabled = true;
        try {
            const response = await fetch(scanForm.action, {
                method: 'POST',
                body: new FormData(scanForm),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) {
                const result = await response.json();
                throw new Error(result.message || 'Không thể lưu Serial.');
            }
            const html = await response.text();
            const replacement = new DOMParser().parseFromString(html, 'text/html').querySelector('[data-return-serial-list]');
            if (!replacement) throw new Error('Không cập nhật được danh sách Serial. Hãy tải lại trang để kiểm tra.');
            document.querySelector('[data-return-serial-list]').replaceWith(replacement);
            const version = response.headers.get('X-Receipt-Version');
            if (version) document.querySelectorAll('input[name="version"]').forEach(field => { field.value = version; });
            const count = response.headers.get('X-Received-Count');
            const counter = document.querySelector('[data-received-count]');
            if (count !== null && counter) counter.textContent = `${counter.dataset.requested} / ${count}`;
            input.value = '';
            note.value = '';
            condition.value = 'GOOD';
            reset();
            clearNoteError();
            saveSuccess.textContent = 'Đã thêm Serial vào phiếu nháp.';
            saveSuccess.hidden = false;
        } catch (e) {
            saveError.textContent = e.message || 'Không thể lưu Serial.';
            saveError.hidden = false;
            addButton.disabled = false;
        }
    });
}
