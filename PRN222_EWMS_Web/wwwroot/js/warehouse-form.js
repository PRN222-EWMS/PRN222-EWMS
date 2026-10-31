(() => {
    const form = document.getElementById('warehouse-form');
    if (!form) return;
    const type = form.querySelector('#WarehouseType');
    const rental = form.querySelector('#warehouse-rental-fields');
    const updateRental = () => {
        const rented = type.value === 'RENTED';
        rental.hidden = !rented;
        rental.disabled = !rented;
        rental.querySelectorAll('input').forEach(input => { input.required = rented; });
    };
    type.addEventListener('change', updateRental);
    updateRental();
})();

(() => {
    const form = document.getElementById('warehouse-location-form');
    if (!form) return;
    const blocked = form.dataset.blocked === 'true';
    const type = form.querySelector('#LocationType').value;
    const zoneType = form.querySelector('#ZoneType');
    const ruleFields = form.querySelector('#warehouse-zone-rules');
    const rows = form.querySelector('#warehouse-rule-rows');
    const addButton = form.querySelector('#warehouse-add-rule');
    const template = form.querySelector('#warehouse-rule-template');
    const limitMessage = form.querySelector('#warehouse-rule-limit');

    const reindexRules = () => {
        const ruleRows = rows.querySelectorAll('.warehouse-rule-row');
        ruleRows.forEach((row, index) => {
            row.querySelectorAll('[data-rule-field]').forEach(select => {
                const field = select.dataset.ruleField;
                select.name = `Rules[${index}].${field}`;
                select.id = `Rules_${index}__${field}`;
                select.parentElement.querySelector('label').htmlFor = select.id;
                const validation = select.parentElement.querySelector('[data-valmsg-for]');
                if (validation) validation.dataset.valmsgFor = select.name;
            });
        });
        addButton.disabled = blocked || ruleRows.length >= 100;
        limitMessage.textContent = ruleRows.length >= 100 ? 'Tối đa 100 cặp loại hàng và hãng.' : '';
    };
    const addRule = () => {
        if (blocked || rows.children.length >= 100) return;
        rows.append(template.content.cloneNode(true));
        reindexRules();
    };
    if (zoneType && ruleFields) {
        const updateRules = () => {
            const standard = zoneType.value === 'STANDARD';
            ruleFields.hidden = !standard;
            ruleFields.disabled = blocked || !standard;
            if (standard && rows.children.length === 0) addRule();
        };
        zoneType.addEventListener('change', updateRules);
        addButton.addEventListener('click', () => {
            addRule();
            rows.lastElementChild?.querySelector('select')?.focus();
        });
        rows.addEventListener('click', event => {
            const removeButton = event.target.closest('[data-remove-rule]');
            if (!removeButton || blocked) return;
            removeButton.closest('.warehouse-rule-row').remove();
            reindexRules();
        });
        reindexRules();
        updateRules();
    }

    const preview = form.querySelector('#warehouse-capacity-preview');
    if (!preview) return;
    const length = form.querySelector('#LengthCm');
    const width = form.querySelector('#WidthCm');
    const height = form.querySelector('#HeightCm');
    const format = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 6 });
    const updateCapacity = () => {
        const l = length.valueAsNumber;
        const w = width.valueAsNumber;
        const h = height.valueAsNumber;
        if (!(l > 0 && w > 0) || (type === 'TIER' && !(h > 0))) {
            preview.textContent = 'Nhập đủ kích thước dương để xem sức chứa cấu hình.';
            return;
        }
        preview.textContent = type === 'TIER'
            ? `Thể tích sử dụng: ${format.format(l * w * h / 1000000)} m³.`
            : `Diện tích sử dụng: ${format.format(l * w / 10000)} m².`;
    };
    [length, width, height].forEach(input => input.addEventListener('input', updateCapacity));
    updateCapacity();
})();
