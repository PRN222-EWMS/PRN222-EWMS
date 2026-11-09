(() => {
    'use strict';

    const form = document.getElementById('manager-stocktake-form');
    if (!form) return;

    const warehouse = document.getElementById('manager-stocktake-plan-warehouse');
    const scopeType = document.getElementById('manager-stocktake-scope-type');
    const includeAwaiting = document.getElementById('manager-stocktake-include-awaiting');
    const forcedAwaiting = document.getElementById('manager-stocktake-forced-awaiting');
    const partialScope = document.getElementById('manager-stocktake-partial-scope');
    const scopeOptions = document.getElementById('manager-stocktake-scope-options');
    const search = document.getElementById('manager-stocktake-location-search');
    const rows = document.getElementById('manager-stocktake-assignment-rows');
    const addButton = document.getElementById('manager-stocktake-add-assignment');
    const notice = document.getElementById('manager-stocktake-form-notice');
    const emptyAssignments = document.getElementById('manager-stocktake-empty-assignments');
    const noStaff = document.getElementById('manager-stocktake-no-staff');
    const scopeSummary = document.getElementById('manager-stocktake-scope-summary');
    const selectedScopes = document.getElementById('manager-stocktake-selected-scopes');
    const noScopeMatch = document.getElementById('manager-stocktake-scope-no-match');
    let planning = JSON.parse(document.getElementById('manager-stocktake-planning-data').textContent);
    const explicitScopeIds = new Set((planning.scopeLocationIds || []).map(Number));
    let expansionBeforeSearch = null;
    let activeWarehouse = Number(warehouse.value);
    let currentRequest = null;
    let requestNumber = 0;
    let loading = false;

    const locationMap = () => new Map(planning.locations.map(location => [Number(location.id), location]));
    const selectedRoots = () => [...explicitScopeIds];
    const locationType = type => ({ ZONE: 'Zone', ROW: 'Dãy', RACK: 'Kệ', TIER: 'Tầng', FLOOR_SLOT: 'Ô sàn' })[type] || type;
    const ancestorIn = (id, selected, map) => {
        let parent = map.get(id)?.parentId;
        const visited = new Set([id]);
        while (parent && !visited.has(Number(parent))) {
            parent = Number(parent);
            if (selected.has(parent)) return parent;
            visited.add(parent);
            parent = map.get(parent)?.parentId;
        }
        return null;
    };
    const notify = (message, error = false) => {
        notice.textContent = message;
        notice.classList.toggle('d-none', !message);
        notice.classList.toggle('alert-danger', error);
        notice.classList.toggle('alert-info', !error);
        notice.setAttribute('role', error ? 'alert' : 'status');
    };
    const option = (value, text) => {
        const element = document.createElement('option');
        element.value = String(value);
        element.textContent = text;
        return element;
    };
    const awaitingInScope = () => scopeType.value === 'WHOLE_WAREHOUSE' || includeAwaiting.checked;
    const assignmentKindOptions = () => [
        option('LOCATION', 'Nhánh vị trí'),
        ...(awaitingInScope() ? [option('AWAITING_PUTAWAY', 'Chỉ hàng chờ xếp')] : []),
        option('ALL_SCOPE', 'Toàn phạm vi đợt')
    ];
    const allowedLocations = () => {
        if (scopeType.value === 'WHOLE_WAREHOUSE') return planning.locations;
        const roots = new Set(selectedRoots());
        const map = locationMap();
        return planning.locations.filter(location => {
            let cursor = Number(location.id);
            const visited = new Set();
            while (cursor && !visited.has(cursor)) {
                if (roots.has(cursor)) return true;
                visited.add(cursor);
                cursor = Number(map.get(cursor)?.parentId || 0);
            }
            return false;
        });
    };

    function updateAssignment(row) {
        const select = row.querySelector('[data-assignment-location]');
        const kind = row.querySelector('[data-assignment-kind]').value;
        const oldValue = select.value;
        const previousLabel = select.selectedOptions[0]?.textContent.replace(/ \(ngoài phạm vi hoặc không còn hợp lệ\)$/, '');
        select.replaceChildren(option('', kind === 'LOCATION' ? 'Chọn nhánh vị trí' : 'Không áp dụng'));
        if (kind === 'LOCATION') {
            const locations = allowedLocations();
            if (oldValue && !locations.some(location => Number(location.id) === Number(oldValue))) {
                select.append(option(oldValue, (previousLabel || `Vị trí #${oldValue}`) + ' (ngoài phạm vi hoặc không còn hợp lệ)'));
            }
            locations.forEach(location => select.append(option(location.id, location.path || location.name)));
            select.value = oldValue;
        }
        select.disabled = kind !== 'LOCATION';
    }

    function syncAssignmentKinds() {
        rows.querySelectorAll('[data-assignment-row]').forEach(row => {
            const select = row.querySelector('[data-assignment-kind]');
            const current = select.value;
            if (current === 'AWAITING_PUTAWAY' && !awaitingInScope()) {
                row.remove();
                return;
            }
            const options = assignmentKindOptions();
            if (current && !options.some(item => item.value === current)) options.unshift(option(current, 'Loại không hợp lệ'));
            select.replaceChildren(...options);
            select.value = current || 'LOCATION';
            updateAssignment(row);
        });
        reindexAssignments();
    }

    function reindexAssignments() {
        rows.querySelectorAll('[data-assignment-row]').forEach((row, index) => {
            [['user', 'UserId'], ['kind', 'ScopeKind'], ['location', 'LocationId']].forEach(([part, property]) => {
                const select = row.querySelector(`[data-assignment-${part}]`);
                const label = row.querySelector(`label[data-label-${part}]`) || select.previousElementSibling;
                select.name = `Assignments[${index}].${property}`;
                select.id = `manager-stocktake-assignment-${part}-${index}`;
                if (label?.tagName === 'LABEL') label.htmlFor = select.id;
            });
            row.querySelector('[data-remove-assignment]').setAttribute('aria-label', `Xóa phân công ${index + 1}`);
        });
        emptyAssignments.classList.toggle('d-none', rows.children.length > 0);
    }

    function updateScope() {
        const whole = scopeType.value === 'WHOLE_WAREHOUSE';
        partialScope.classList.toggle('d-none', whole);
        if (whole) includeAwaiting.checked = true;
        includeAwaiting.disabled = whole;
        forcedAwaiting.disabled = !whole;
        syncScopeSelection();
        syncAssignmentKinds();
    }

    function syncScopeSelection() {
        const map = locationMap();
        for (const id of explicitScopeIds) {
            if (!map.has(id) || ancestorIn(id, explicitScopeIds, map)) explicitScopeIds.delete(id);
        }
        const whole = scopeType.value === 'WHOLE_WAREHOUSE';
        const partialAncestors = new Set();
        for (const id of explicitScopeIds) {
            let parent = map.get(id)?.parentId;
            const visited = new Set([id]);
            while (parent && !visited.has(Number(parent))) {
                parent = Number(parent);
                partialAncestors.add(parent);
                visited.add(parent);
                parent = map.get(parent)?.parentId;
            }
        }
        scopeOptions.querySelectorAll('[data-scope-node]').forEach(node => {
            const id = Number(node.dataset.locationId);
            const coveredBy = ancestorIn(id, explicitScopeIds, map);
            const selected = explicitScopeIds.has(id);
            const partial = !selected && !coveredBy && partialAncestors.has(id);
            const input = node.querySelector('[data-scope-input]');
            const state = node.querySelector('[data-scope-state]');
            input.checked = selected || !!coveredBy;
            input.indeterminate = partial;
            input.disabled = whole || loading || !!coveredBy;
            input.setAttribute('aria-checked', partial ? 'mixed' : String(input.checked));
            input.title = coveredBy ? `Đã bao gồm trong ${map.get(coveredBy).path || map.get(coveredBy).name}` : '';
            node.querySelector('[data-scope-select]').classList.toggle('text-muted', !!coveredBy);
            state.textContent = coveredBy ? 'Đã bao gồm' : selected ? 'Đã chọn' : partial ? 'Chọn một phần' : '';
            state.classList.toggle('d-none', !state.textContent);
            state.classList.toggle('text-bg-primary', selected);
            state.classList.toggle('text-bg-light', !selected);
        });
        scopeSummary.textContent = explicitScopeIds.size ? `Đã chọn ${explicitScopeIds.size} nhánh` : 'Chưa chọn nhánh';
        selectedScopes.replaceChildren();
        for (const id of explicitScopeIds) {
            const remove = document.createElement('button');
            const location = map.get(id);
            remove.type = 'button';
            remove.className = 'btn btn-sm btn-outline-primary rounded-pill';
            remove.dataset.removeScope = String(id);
            remove.disabled = loading;
            remove.setAttribute('aria-label', `Bỏ chọn ${location.path || location.name}`);
            const label = document.createElement('span');
            label.textContent = location.path || location.name;
            const icon = document.createElement('i');
            icon.className = 'bi bi-x-lg ms-2';
            icon.setAttribute('aria-hidden', 'true');
            remove.append(label, icon);
            selectedScopes.append(remove);
        }
    }

    function renderScope() {
        scopeOptions.replaceChildren();
        expansionBeforeSearch = null;
        const children = new Map();
        for (const location of planning.locations) {
            const parentId = Number(location.parentId || 0);
            if (!children.has(parentId)) children.set(parentId, []);
            children.get(parentId).push(location);
        }
        function renderNode(location) {
            const nested = children.get(Number(location.id)) || [];
            const container = document.createElement(nested.length ? 'details' : 'div');
            container.className = nested.length ? 'warehouse-tree-node mt-2' : 'border rounded px-3 py-2 mt-2';
            container.dataset.scopeNode = '';
            container.dataset.locationId = String(location.id);
            const heading = nested.length ? document.createElement('summary') : container;
            const label = document.createElement('label');
            label.className = 'd-inline-flex align-items-center gap-2 mb-0 text-break';
            label.dataset.scopeSelect = '';
            const input = document.createElement('input');
            input.type = 'checkbox';
            input.className = 'form-check-input m-0 flex-shrink-0';
            input.name = 'ScopeLocationIds';
            input.id = `manager-stocktake-scope-${location.id}`;
            input.value = String(location.id);
            input.dataset.scopeInput = '';
            const name = document.createElement('span');
            name.textContent = location.name;
            label.append(input, name);
            const type = document.createElement('span');
            type.className = 'badge rounded-pill text-bg-light ms-2';
            type.textContent = locationType(location.type);
            const state = document.createElement('span');
            state.className = 'badge rounded-pill ms-2 d-none';
            state.dataset.scopeState = '';
            heading.append(label, type, state);
            if (nested.length) {
                const content = document.createElement('div');
                content.className = 'warehouse-node-content';
                nested.forEach(child => content.append(renderNode(child)));
                container.append(heading, content);
            }
            return container;
        }
        (children.get(0) || []).forEach(location => scopeOptions.append(renderNode(location)));
        if (!planning.locations.length) {
            const empty = document.createElement('p');
            empty.className = 'muted mb-0';
            empty.textContent = 'Chưa có vị trí để chọn. Hãy chọn kho có cấu trúc hoặc lập phạm vi toàn kho.';
            scopeOptions.append(empty);
        }
        search.value = '';
        noScopeMatch.classList.add('d-none');
    }

    function filterScope() {
        const term = search.value.trim().toLocaleLowerCase('vi');
        const branches = [...scopeOptions.querySelectorAll('details[data-scope-node]')];
        if (!term) {
            scopeOptions.querySelectorAll('[data-scope-node]').forEach(node => node.classList.remove('d-none'));
            if (expansionBeforeSearch) branches.forEach(branch => { branch.open = expansionBeforeSearch.get(branch.dataset.locationId) || false; });
            expansionBeforeSearch = null;
            noScopeMatch.classList.add('d-none');
            return;
        }
        expansionBeforeSearch ??= new Map(branches.map(branch => [branch.dataset.locationId, branch.open]));
        const map = locationMap();
        const matches = new Set(planning.locations.filter(location => (location.path || location.name).toLocaleLowerCase('vi').includes(term)).map(location => Number(location.id)));
        const matchRoots = new Set([...matches].filter(id => !ancestorIn(id, matches, map)));
        const visible = new Set();
        const expand = new Set();
        for (const location of planning.locations) {
            const id = Number(location.id);
            if (matchRoots.has(id) || ancestorIn(id, matchRoots, map)) visible.add(id);
        }
        for (const id of matchRoots) {
            let parent = map.get(id)?.parentId;
            const visited = new Set([id]);
            while (parent && !visited.has(Number(parent))) {
                parent = Number(parent);
                visible.add(parent);
                expand.add(parent);
                visited.add(parent);
                parent = map.get(parent)?.parentId;
            }
        }
        scopeOptions.querySelectorAll('[data-scope-node]').forEach(node => node.classList.toggle('d-none', !visible.has(Number(node.dataset.locationId))));
        branches.forEach(branch => { branch.open = expand.has(Number(branch.dataset.locationId)) || expansionBeforeSearch.get(branch.dataset.locationId) || false; });
        noScopeMatch.classList.toggle('d-none', matches.size > 0 || planning.locations.length === 0);
    }

    function addAssignment() {
        const row = document.createElement('tr');
        row.dataset.assignmentRow = '';
        const definitions = [
            ['user', 'Nhân viên', [option(0, 'Chọn nhân viên'), ...planning.staff.map(staff => option(staff.id, staff.name))]],
            ['kind', 'Loại phân công', assignmentKindOptions()],
            ['location', 'Nhánh vị trí', [option('', 'Chọn nhánh vị trí')]]
        ];
        definitions.forEach(([part, title, options]) => {
            const cell = document.createElement('td');
            const label = document.createElement('label');
            label.className = 'visually-hidden';
            label.dataset[`label${part[0].toUpperCase()}${part.slice(1)}`] = '';
            label.textContent = title;
            const select = document.createElement('select');
            select.dataset[`assignment${part[0].toUpperCase()}${part.slice(1)}`] = '';
            select.append(...options);
            cell.append(label, select);
            row.append(cell);
        });
        const actions = document.createElement('td');
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'btn btn-outline-danger';
        remove.dataset.removeAssignment = '';
        const icon = document.createElement('i');
        icon.className = 'bi bi-trash';
        icon.setAttribute('aria-hidden', 'true');
        remove.append(icon);
        actions.append(remove);
        row.append(actions);
        rows.append(row);
        updateAssignment(row);
        reindexAssignments();
        row.querySelector('select').focus();
    }

    function setLoading(value) {
        loading = value;
        form.querySelectorAll('[data-save-plan]').forEach(button => { button.disabled = value; });
        addButton.disabled = value || activeWarehouse <= 0;
        scopeType.disabled = value;
        form.querySelectorAll('[data-expand-scope], [data-collapse-scope]').forEach(button => { button.disabled = value; });
        form.setAttribute('aria-busy', String(value));
        syncScopeSelection();
    }

    async function changeWarehouse() {
        const nextWarehouse = Number(warehouse.value);
        const sequence = ++requestNumber;
        currentRequest?.abort();
        if (nextWarehouse === activeWarehouse) { setLoading(false); return; }
        if (nextWarehouse <= 0) {
            activeWarehouse = 0;
            planning = { locations: [], staff: [] };
            explicitScopeIds.clear();
            rows.replaceChildren();
            renderScope();
            reindexAssignments();
            updateScope();
            noStaff.classList.add('d-none');
            setLoading(false);
            notify('Chưa chọn kho. Phạm vi và phân công trước đó đã được đặt lại.');
            return;
        }
        currentRequest = new AbortController();
        setLoading(true);
        notify('Đang tải cấu trúc kho và danh sách nhân viên...');
        try {
            const url = new URL(form.dataset.planningUrl, window.location.origin);
            url.searchParams.set('warehouseId', String(nextWarehouse));
            const response = await fetch(url, { signal: currentRequest.signal, headers: { 'Accept': 'application/json' } });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || data.error || 'Không tải được thông tin kho. Vui lòng thử lại.');
            if (!Array.isArray(data.locations) || !Array.isArray(data.staff)) throw new Error('Thông tin kho không hợp lệ. Vui lòng tải lại trang.');
            if (sequence !== requestNumber) return;
            planning = data;
            activeWarehouse = nextWarehouse;
            explicitScopeIds.clear();
            rows.replaceChildren();
            renderScope();
            reindexAssignments();
            updateScope();
            noStaff.classList.toggle('d-none', data.staff.length > 0);
            const warnings = Array.isArray(data.warnings) && data.warnings.length ? ` ${data.warnings.join(' ')}` : '';
            notify('Đã tải kho mới. Phạm vi và phân công trước đó đã được đặt lại; vui lòng chọn lại.' + warnings);
        } catch (error) {
            if (error.name === 'AbortError' || sequence !== requestNumber) return;
            warehouse.value = String(activeWarehouse);
            notify(error instanceof SyntaxError ? 'Không tải được thông tin kho. Phiên đăng nhập có thể đã hết hạn; vui lòng tải lại trang.' : error.message, true);
        } finally {
            if (sequence === requestNumber) setLoading(false);
        }
    }

    if (form.dataset.editing !== 'true') warehouse.addEventListener('change', changeWarehouse);
    scopeType.addEventListener('change', updateScope);
    includeAwaiting.addEventListener('change', syncAssignmentKinds);
    scopeOptions.addEventListener('change', event => {
        if (!event.target.matches('[data-scope-input]')) return;
        const id = Number(event.target.value);
        if (event.target.checked) {
            const map = locationMap();
            for (const selected of explicitScopeIds) {
                if (ancestorIn(selected, new Set([id]), map)) explicitScopeIds.delete(selected);
            }
            explicitScopeIds.add(id);
            const branch = event.target.closest('details[data-scope-node]');
            if (branch && Number(branch.dataset.locationId) === id && !search.value.trim()) branch.open = false;
        } else explicitScopeIds.delete(id);
        syncScopeSelection();
        rows.querySelectorAll('[data-assignment-row]').forEach(updateAssignment);
    });
    // Choosing a checkbox/label should not also toggle its enclosing <details>.
    scopeOptions.addEventListener('click', event => {
        const label = event.target.closest('[data-scope-select]');
        if (!label) return;
        event.stopPropagation();
        if (!event.target.matches('[data-scope-input]')) {
            event.preventDefault();
            const input = label.querySelector('[data-scope-input]');
            if (!input.disabled) input.click();
        }
    });
    selectedScopes.addEventListener('click', event => {
        const remove = event.target.closest('[data-remove-scope]');
        if (!remove) return;
        explicitScopeIds.delete(Number(remove.dataset.removeScope));
        syncScopeSelection();
        rows.querySelectorAll('[data-assignment-row]').forEach(updateAssignment);
    });
    search.addEventListener('input', filterScope);
    form.querySelector('[data-expand-scope]').addEventListener('click', () => scopeOptions.querySelectorAll('details[data-scope-node]').forEach(branch => { branch.open = true; }));
    form.querySelector('[data-collapse-scope]').addEventListener('click', () => scopeOptions.querySelectorAll('details[data-scope-node]').forEach(branch => { branch.open = false; }));
    document.querySelectorAll('[data-manager-stocktake-back]').forEach(link => link.addEventListener('click', event => {
        if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !document.referrer || window.history.length <= 1) return;
        const previous = new URL(document.referrer);
        if (previous.origin !== window.location.origin || previous.pathname === window.location.pathname || previous.pathname.startsWith('/Authentication')) return;
        event.preventDefault();
        window.history.back();
    }));
    addButton.addEventListener('click', addAssignment);
    rows.addEventListener('change', event => {
        if (event.target.matches('[data-assignment-kind]')) updateAssignment(event.target.closest('[data-assignment-row]'));
    });
    rows.addEventListener('click', event => {
        const remove = event.target.closest('[data-remove-assignment]');
        if (!remove) return;
        remove.closest('[data-assignment-row]').remove();
        reindexAssignments();
    });
    form.addEventListener('submit', event => {
        if (loading) { event.preventDefault(); notify('Đợi tải xong cấu trúc kho trước khi lưu.', true); return; }
        // Preserve the selected submit action before disabling buttons to prevent double clicks.
        const mode = document.createElement('input');
        mode.type = 'hidden';
        mode.name = 'SaveMode';
        mode.value = event.submitter?.value || 'DRAFT';
        form.append(mode);
        form.querySelectorAll('[data-save-plan]').forEach(button => { button.disabled = true; });
    });

    updateScope();
    const initialMap = locationMap();
    scopeOptions.querySelectorAll('details[data-scope-node]').forEach(branch => {
        const id = Number(branch.dataset.locationId);
        branch.open = [...explicitScopeIds].some(selected => ancestorIn(selected, new Set([id]), initialMap));
    });
    reindexAssignments();
    setLoading(false);
})();
