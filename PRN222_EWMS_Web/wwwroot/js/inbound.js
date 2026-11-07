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
    const serial = form.querySelector('[data-serial-input]');
    const method = form.querySelector('[data-capture-method]');
    const source = form.querySelector('[data-capture-source]');
    const message = form.querySelector('[data-capture-message]');
    const candidateBox = form.querySelector('[data-candidates]');
    const cameraBox = form.querySelector('[data-camera-box]');
    const video = form.querySelector('[data-camera-video]');
    const open = form.querySelector('[data-camera-open]');
    const close = form.querySelector('[data-camera-close]');
    const retry = form.querySelector('[data-capture-retry]');
    const upload = form.querySelector('[data-image-upload]');
    const month = form.querySelector('[name="NewSerial.Month"]');
    const year = form.querySelector('[name="NewSerial.Year"]');
    const keep = form.querySelector('[data-keep-production]');
    let stream = null, timer = null, zxingControls = null, scanning = false, lastRead = '', stableReads = 0;

    function setCapture(value, captureMethod) {
        serial.value = value;
        method.value = captureMethod;
        source.textContent = `Nguồn: ${captureMethod === 'CAMERA' ? 'Camera' : captureMethod === 'IMAGE_UPLOAD' ? 'Ảnh tem' : 'Nhập tay'} · Kiểm tra trước khi lưu.`;
        serial.focus();
    }
    function showChoices(values, captureMethod, explanation) {
        candidateBox.replaceChildren();
        const unique = [...new Set(values.map(value => value?.trim()).filter(value => value && value.length <= 100 && !/[\x00-\x1f\x7f]/.test(value)))];
        message.textContent = explanation;
        if (!unique.length) return;
        const list = document.createElement('div');
        list.className = 'inbound-candidate-list';
        unique.slice(0, 12).forEach(value => {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = value;
            button.addEventListener('click', () => { setCapture(value, captureMethod); candidateBox.replaceChildren(); message.textContent = 'Đã chọn kết quả. Đối chiếu với tem trước khi lưu.'; });
            list.append(button);
        });
        candidateBox.append(list);
    }
    function stopCamera() {
        clearInterval(timer); timer = null;
        zxingControls?.stop(); zxingControls = null;
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
        video.srcObject = null;
        cameraBox.hidden = true;
        open.disabled = false;
        lastRead = ''; stableReads = 0; scanning = false;
    }
    close.addEventListener('click', stopCamera);
    window.addEventListener('pagehide', stopCamera);
    document.addEventListener('visibilitychange', () => { if (document.hidden) stopCamera(); });
    retry.addEventListener('click', () => { stopCamera(); serial.value = ''; method.value = 'MANUAL'; source.textContent = 'Nguồn: Nhập tay · Enter để thêm nhanh.'; candidateBox.replaceChildren(); message.textContent = ''; message.classList.remove('is-error', 'is-success'); upload.value = ''; serial.focus(); });
    serial.addEventListener('input', event => { if (event.isTrusted && !serial.value.trim()) { method.value = 'MANUAL'; source.textContent = 'Nguồn: Nhập tay · Enter để thêm nhanh.'; } });

    async function detector() {
        if (!window.BarcodeDetector) return null;
        const supported = await BarcodeDetector.getSupportedFormats?.() ?? [];
        const preferred = ['code_128', 'code_39', 'ean_13', 'ean_8', 'data_matrix', 'qr_code'];
        const formats = preferred.filter(format => supported.includes(format));
        return formats.length ? new BarcodeDetector({ formats }) : new BarcodeDetector();
    }
    open.addEventListener('click', async () => {
        if (!navigator.mediaDevices?.getUserMedia || (!window.BarcodeDetector && !window.ZXingBrowser)) {
            message.textContent = 'Trình duyệt không hỗ trợ quét mã hoặc camera không khả dụng. Bạn có thể nhập Serial thủ công.';
            return;
        }
        try {
            if (window.ZXingBrowser) {
                cameraBox.hidden = false;
                open.disabled = true;
                message.textContent = 'Đưa mã vạch vào giữa khung và giữ yên.';
                const reader = new ZXingBrowser.BrowserMultiFormatOneDReader();
                zxingControls = await reader.decodeFromConstraints({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } } }, video, result => {
                    const value = result?.getText()?.trim();
                    if (!value) return;
                    stableReads = value === lastRead ? stableReads + 1 : 1;
                    lastRead = value;
                    if (stableReads >= 2) { stopCamera(); setCapture(value, 'CAMERA'); message.textContent = 'Đã đọc mã vạch. Kiểm tra Serial trên tem trước khi lưu.'; }
                });
                return;
            }
            const reader = await detector();
            stream = await navigator.mediaDevices.getUserMedia({ audio: false, video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } } });
            video.srcObject = stream;
            cameraBox.hidden = false;
            open.disabled = true;
            await video.play();
            message.textContent = 'Đưa mã vào giữa khung, giữ yên vài giây.';
            timer = setInterval(async () => {
                if (scanning || video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA) return;
                scanning = true;
                try {
                    const codes = await reader.detect(video);
                    const values = [...new Set(codes.map(code => code.rawValue?.trim()).filter(Boolean))];
                    if (values.length !== 1) {
                        lastRead = ''; stableReads = 0;
                        if (values.length > 1) { stopCamera(); showChoices(values, 'CAMERA', 'Camera đọc được nhiều mã. Chọn đúng Serial trên tem.'); }
                        return;
                    }
                    stableReads = values[0] === lastRead ? stableReads + 1 : 1;
                    lastRead = values[0];
                    if (stableReads >= 3) { stopCamera(); setCapture(values[0], 'CAMERA'); message.textContent = 'Đã quét ổn định. Kiểm tra Serial với tem trước khi lưu.'; }
                } catch { /* A frame may be unreadable while the camera focuses. */ }
                finally { scanning = false; }
            }, 450);
        } catch { stopCamera(); message.textContent = 'Không mở được camera. Kiểm tra quyền truy cập và dùng HTTPS hoặc localhost.'; }
    });

    upload.addEventListener('change', async () => {
        const file = upload.files?.[0];
        if (!file) return;
        if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 10 * 1024 * 1024) {
            message.textContent = 'Chỉ chọn JPEG, PNG hoặc WebP tối đa 10 MB.'; upload.value = ''; return;
        }
        stopCamera();
        candidateBox.replaceChildren();
        message.textContent = 'Đang đọc ảnh tem…';
        let barcodeValues = [], ocrValues = [];
        try {
            const reader = await detector();
            if (reader) {
                const bitmap = await createImageBitmap(file);
                try { barcodeValues = (await reader.detect(bitmap)).map(result => result.rawValue?.trim()).filter(Boolean); }
                finally { bitmap.close(); }
            }
        } catch { /* OCR may still recover the printed serial. */ }
        if (!barcodeValues.length && window.ZXingBrowser) {
            const objectUrl = URL.createObjectURL(file);
            try {
                const result = await new ZXingBrowser.BrowserMultiFormatOneDReader().decodeFromImageUrl(objectUrl);
                if (result?.getText()) barcodeValues = [result.getText().trim()];
            } catch { /* OCR may still recover the printed serial. */ }
            finally { URL.revokeObjectURL(objectUrl); }
        }
        try {
            if (window.Tesseract) {
                const worker = await Tesseract.createWorker('eng');
                try {
                    const bitmap = await createImageBitmap(file);
                    const crop = document.createElement('canvas');
                    crop.width = bitmap.width;
                    crop.height = Math.round(bitmap.height * .72);
                    crop.getContext('2d').drawImage(bitmap, 0, 0);
                    bitmap.close();
                    const text = (await worker.recognize(crop)).data.text || '';
                    const labelled = [...text.matchAll(/(?:S\s*\/?\s*N|SERIA[L1I](?:\s*(?:NO\.?|NUMBER|#))?)\s*[:#-]?\s*([A-Z0-9][A-Z0-9-]{7,99})/gi)];
                    ocrValues = labelled.map(match => match[1]);
                    if (!ocrValues.length) {
                        ocrValues = text.split(/[^A-Z0-9-]+/i).filter(value => value.length >= 10 && value.length <= 40 && /[A-Z]/i.test(value) && /\d/.test(value));
                    }
                } finally { await worker.terminate(); }
            }
        } catch { /* Manual entry stays available. */ }
        const values = [...new Set([...barcodeValues, ...ocrValues])];
        if (values.length === 1) {
            setCapture(values[0], 'IMAGE_UPLOAD');
            message.textContent = 'Đã đọc một mã từ ảnh. Đối chiếu với Serial trên tem trước khi lưu.';
        } else if (values.length > 1) {
            showChoices(values, 'IMAGE_UPLOAD', 'Mã vạch và chữ trên ảnh cho nhiều kết quả. Chọn đúng Serial trên tem.');
        } else {
            message.textContent = 'Không đọc được Serial rõ ràng từ ảnh. Vui lòng nhập thủ công.';
        }
    });

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
        button.disabled = true;
        button.textContent = 'Đang lưu Serial…';
        message.classList.remove('is-error', 'is-success');
        message.textContent = '';
        try {
            const response = await fetch(form.action, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
            });
            if (!response.headers.get('content-type')?.includes('application/json'))
                throw new Error('Phiên đăng nhập đã hết hạn hoặc yêu cầu bị từ chối. Vui lòng đăng nhập lại.');
            const result = await response.json();
            if (!response.ok || !result.ok) throw new Error(result.message || 'Không thể lưu Serial. Vui lòng thử lại.');
            serial.value = '';
            form.querySelector('[name="NewSerial.Condition"]').value = 'GOOD';
            form.querySelector('[name="NewSerial.ConditionNote"]').value = '';
            method.value = 'MANUAL';
            source.textContent = 'Nguồn: Nhập tay · Enter để thêm nhanh.';
            candidateBox.replaceChildren();
            upload.value = '';
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
            button.disabled = false;
            button.textContent = originalLabel;
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
