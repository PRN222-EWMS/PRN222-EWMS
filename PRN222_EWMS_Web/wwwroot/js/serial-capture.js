(() => {
    const instances = new WeakMap();
    function create(root) {
        if (instances.has(root)) return instances.get(root);
        const input = root.querySelector('[data-serial-input]');
        const method = root.querySelector('[data-capture-method]');
        const source = root.querySelector('[data-capture-source]');
        const message = root.querySelector('[data-capture-message]');
        const choices = root.querySelector('[data-candidates]');
        const box = root.querySelector('[data-camera-box]');
        const video = root.querySelector('[data-camera-video]');
        const open = root.querySelector('[data-camera-open]');
        const close = root.querySelector('[data-camera-close]');
        const retry = root.querySelector('[data-capture-retry]');
        const upload = root.querySelector('[data-image-upload]');
        let generation = 0, disabled = false, imageBusy = false, timer = null, stream = null, controls = null, worker = null;
        let lastRead = '', stableReads = 0, detecting = false;
        const changed = () => root.dispatchEvent(new CustomEvent('serialcapture:changed', { bubbles: true, detail: { value: input.value, method: method.value } }));
        function updateControls() {
            open.disabled = disabled || imageBusy || !box.hidden;
            upload.disabled = disabled || imageBusy;
            upload.closest('label').classList.toggle('is-disabled', upload.disabled);
            input.disabled = disabled;
            retry.disabled = disabled;
            root.setAttribute('aria-busy', imageBusy ? 'true' : 'false');
            choices.querySelectorAll('button').forEach(button => { button.disabled = disabled || imageBusy; });
        }
        function stop() {
            generation++;
            clearInterval(timer); timer = null;
            try { controls?.stop(); } catch { /* Camera may already be closed by the browser. */ }
            controls = null;
            stream?.getTracks().forEach(track => track.stop()); stream = null;
            if (typeof MediaStream !== 'undefined' && video.srcObject instanceof MediaStream) video.srcObject.getTracks().forEach(track => track.stop());
            video.srcObject = null; box.hidden = true;
            if (worker) { const previous = worker; worker = null; previous.terminate().catch(() => {}); }
            imageBusy = false; lastRead = ''; stableReads = 0; detecting = false;
            updateControls();
        }
        function setValue(value, captureMethod) {
            value = value?.trim();
            if (disabled || !value || value.length > 100 || /[\x00-\x1f\x7f]/.test(value)) return;
            input.value = value; method.value = captureMethod;
            source.textContent = `Nguồn: ${captureMethod === 'CAMERA' ? 'Camera' : captureMethod === 'IMAGE_UPLOAD' ? 'Ảnh tem' : 'Nhập tay'} · Đối chiếu Serial trên tem.`;
            input.focus({ preventScroll: true }); changed();
        }
        function showChoices(values, captureMethod) {
            choices.replaceChildren();
            const unique = [...new Set(values.map(value => value?.trim()).filter(value => value && value.length <= 100 && !/[\x00-\x1f\x7f]/.test(value)))];
            if (!unique.length) { message.textContent = 'Chưa đọc được Serial rõ ràng. Thử ảnh rõ hơn hoặc nhập tay.'; return; }
            if (unique.length === 1) {
                setValue(unique[0], captureMethod);
                message.textContent = 'Đã điền Serial. Kiểm tra mã trên tem rồi tiếp tục.'; return;
            }
            message.textContent = 'Có nhiều mã trên tem. Hãy chọn đúng Serial.';
            const list = document.createElement('div'); list.className = 'serial-capture-choices';
            unique.slice(0, 12).forEach(value => {
                const button = document.createElement('button'); button.type = 'button'; button.textContent = value;
                button.addEventListener('click', () => { setValue(value, captureMethod); choices.replaceChildren(); message.textContent = 'Đã chọn mã. Đối chiếu với tem trước khi tiếp tục.'; });
                list.append(button);
            });
            choices.append(list); updateControls();
        }
        async function detector() {
            if (!window.BarcodeDetector) return null;
            const supported = await BarcodeDetector.getSupportedFormats?.() ?? [];
            const formats = ['code_128', 'code_39', 'ean_13', 'ean_8', 'data_matrix', 'qr_code'].filter(format => supported.includes(format));
            return formats.length ? new BarcodeDetector({ formats }) : new BarcodeDetector();
        }
        open.addEventListener('click', async () => {
            if (disabled || imageBusy) return;
            stop(); const session = generation;
            choices.replaceChildren(); message.classList.remove('is-error', 'is-success');
            if (!navigator.mediaDevices?.getUserMedia || (!window.BarcodeDetector && !window.ZXingBrowser)) {
                message.textContent = 'Camera không khả dụng. Bạn vẫn có thể dùng máy quét hoặc nhập Serial.'; return;
            }
            try {
                box.hidden = false; updateControls(); message.textContent = 'Đưa mã vào khung và giữ yên để đọc ổn định.';
                if (window.ZXingBrowser) {
                    const Reader = ZXingBrowser.BrowserMultiFormatReader || ZXingBrowser.BrowserMultiFormatOneDReader;
                    const reader = new Reader();
                    const handle = await reader.decodeFromConstraints({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } } }, video, result => {
                        if (session !== generation || disabled || !root.isConnected) return;
                        const value = result?.getText()?.trim(); if (!value) return;
                        stableReads = value === lastRead ? stableReads + 1 : 1; lastRead = value;
                        if (stableReads >= 2) { stop(); showChoices([value], 'CAMERA'); }
                    });
                    if (session !== generation || disabled || !root.isConnected) handle.stop(); else controls = handle;
                    return;
                }
                const reader = await detector();
                if (session !== generation || disabled) return;
                const captured = await navigator.mediaDevices.getUserMedia({ audio: false, video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } } });
                if (session !== generation || disabled || !root.isConnected) { captured.getTracks().forEach(track => track.stop()); return; }
                stream = captured; video.srcObject = stream; await video.play();
                if (session !== generation) return;
                timer = setInterval(async () => {
                    if (detecting || session !== generation || video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA) return;
                    detecting = true;
                    try {
                        const codes = await reader.detect(video);
                        if (session !== generation || disabled) return;
                        const values = [...new Set(codes.map(code => code.rawValue?.trim()).filter(Boolean))];
                        if (values.length !== 1) {
                            lastRead = ''; stableReads = 0;
                            if (values.length > 1) { stop(); showChoices(values, 'CAMERA'); }
                            return;
                        }
                        stableReads = values[0] === lastRead ? stableReads + 1 : 1; lastRead = values[0];
                        if (stableReads >= 3) { stop(); showChoices(values, 'CAMERA'); }
                    } catch { /* The next camera frame may be clearer. */ }
                    finally { if (session === generation) detecting = false; }
                }, 450);
            } catch {
                if (session !== generation) return;
                stop(); message.textContent = 'Không mở được camera. Kiểm tra quyền và dùng HTTPS/localhost, hoặc nhập bằng máy quét/bàn phím.';
            }
        });
        upload.addEventListener('change', async () => {
            const file = upload.files?.[0]; if (!file || disabled) return;
            stop(); choices.replaceChildren(); message.classList.remove('is-error', 'is-success');
            if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 10 * 1024 * 1024) {
                message.textContent = 'Chỉ chọn JPEG, PNG hoặc WebP tối đa 10 MB.'; upload.value = ''; return;
            }
            const session = generation; imageBusy = true; updateControls(); message.textContent = 'Đang đọc ảnh tem…';
            let barcodeValues = [], ocrValues = [];
            try {
                try {
                    const reader = await detector();
                    if (reader) {
                        const bitmap = await createImageBitmap(file);
                        try { barcodeValues = (await reader.detect(bitmap)).map(result => result.rawValue?.trim()).filter(Boolean); }
                        finally { bitmap.close(); }
                    }
                } catch { /* The existing ZXing/OCR fallbacks remain available. */ }
                if (session !== generation || disabled) return;
                if (!barcodeValues.length && window.ZXingBrowser) {
                    const url = URL.createObjectURL(file);
                    try { const Reader = ZXingBrowser.BrowserMultiFormatReader || ZXingBrowser.BrowserMultiFormatOneDReader; const result = await new Reader().decodeFromImageUrl(url); if (result?.getText()) barcodeValues = [result.getText().trim()]; }
                    catch { /* OCR can still read the printed Serial. */ }
                    finally { URL.revokeObjectURL(url); }
                }
                if (session !== generation || disabled) return;
                if (window.Tesseract) {
                    const currentWorker = await Tesseract.createWorker('eng');
                    if (session !== generation || disabled) { await currentWorker.terminate(); return; }
                    worker = currentWorker;
                    try {
                        const bitmap = await createImageBitmap(file); const crop = document.createElement('canvas');
                        crop.width = bitmap.width; crop.height = Math.round(bitmap.height * .72);
                        crop.getContext('2d').drawImage(bitmap, 0, 0); bitmap.close();
                        const text = (await currentWorker.recognize(crop)).data.text || '';
                        const labelled = [...text.matchAll(/(?:S\s*\/?\s*N|SERIA[L1I](?:\s*(?:NO\.?|NUMBER|#))?)\s*[:#-]?\s*([A-Z0-9][A-Z0-9-]{7,99})/gi)];
                        ocrValues = labelled.map(match => match[1]);
                        if (!ocrValues.length) ocrValues = text.split(/[^A-Z0-9-]+/i).filter(value => value.length >= 10 && value.length <= 40 && /[A-Z]/i.test(value) && /\d/.test(value));
                    } finally { if (worker === currentWorker) { worker = null; await currentWorker.terminate(); } }
                }
            } catch { /* Keep barcode results and manual entry usable if OCR fails. */ }
            finally {
                if (session === generation && !disabled && root.isConnected) { imageBusy = false; showChoices([...barcodeValues, ...ocrValues], 'IMAGE_UPLOAD'); updateControls(); }
                upload.value = '';
            }
        });
        function reset() {
            stop(); input.value = ''; method.value = 'MANUAL'; source.textContent = root.dataset.defaultHint;
            choices.replaceChildren(); upload.value = ''; message.textContent = ''; message.classList.remove('is-error', 'is-success'); changed();
        }
        close.addEventListener('click', stop);
        retry.addEventListener('click', () => { reset(); input.focus(); });
        input.addEventListener('input', event => {
            if (!event.isTrusted) return;
            if (imageBusy || !box.hidden) stop();
            choices.replaceChildren(); method.value = 'MANUAL'; source.textContent = root.dataset.defaultHint; changed();
        });
        window.addEventListener('pagehide', stop);
        document.addEventListener('visibilitychange', () => { if (document.hidden) stop(); });
        const api = { stop, reset, setDisabled(value) { if (value) stop(); disabled = value; updateControls(); }, get value() { return input.value; } };
        instances.set(root, api); updateControls(); return api;
    }
    window.EwmsSerialCapture = {
        init(scope = document) { scope.querySelectorAll('[data-serial-capture]').forEach(create); },
        get(root) { return root ? create(root) : null; }
    };
    window.EwmsSerialCapture.init();
})();
