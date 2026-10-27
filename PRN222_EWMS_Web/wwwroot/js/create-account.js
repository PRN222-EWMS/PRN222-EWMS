(() => {
    const form = document.querySelector('form[data-warehouse-role-id]');
    if (!form) return;
    const role = form.querySelector('#RoleId');
    const warehouse = form.querySelector('#WarehouseId');
    if (!role || !warehouse) return;

    const updateWarehouse = () => {
        const enabled = Boolean(form.dataset.warehouseRoleId) && role.value === form.dataset.warehouseRoleId;
        warehouse.disabled = !enabled;
        warehouse.required = enabled;
        warehouse.setAttribute('aria-required', String(enabled));
        warehouse.options[0].textContent = enabled ? 'Chọn kho làm việc' : 'Chỉ dành cho nhân viên kho';
        if (!enabled) warehouse.value = '';
    };
    role.addEventListener('change', updateWarehouse);
    window.addEventListener('pageshow', updateWarehouse);
    updateWarehouse();
})();
