using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Warehouse;
using PRN222_EWMS_Web.ViewModels.Warehouse;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class WarehouseService(IWarehouseRepository warehouses) : IWarehouseService
{
    public async Task<WarehouseListViewModel> GetListAsync(WarehouseListFilter filter, CancellationToken cancellationToken = default)
    {
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var warehouseType = string.IsNullOrWhiteSpace(filter.WarehouseType) ? null : filter.WarehouseType;
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status;
        var result = await warehouses.GetPageAsync(search, warehouseType, status,
            filter.RentalFrom, filter.RentalTo, filter.Page, WarehouseListViewModel.DefaultPageSize, cancellationToken);

        return new WarehouseListViewModel(filter)
        {
            Search = search,
            WarehouseType = warehouseType,
            Status = status,
            Page = result.Page,
            TotalCount = result.TotalCount,
            TotalWarehouses = result.TotalWarehouses,
            DataLoaded = true,
            Items = result.Items.Select(ToListItem).ToList()
        };
    }

    public async Task<WarehouseDetailsViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var warehouse = await warehouses.FindByIdAsync(id, cancellationToken);
        if (warehouse is null) return null;
        var locations = await warehouses.GetLocationsAsync(id, cancellationToken);
        var rules = await warehouses.GetZoneRulesAsync(id, cancellationToken);
        var rulesByZone = rules.ToLookup(r => r.ZoneLocationId);
        var nodes = locations.Select(l => new StorageLocationViewModel
        {
            LocationId = l.LocationId, ParentLocationId = l.ParentLocationId,
            Name = l.LocationName, LocationType = l.LocationType, ZoneType = l.ZoneType,
            StorageType = l.StorageType, ConditionPolicy = l.ConditionPolicy,
            LengthCm = l.LengthCm, WidthCm = l.WidthCm, HeightCm = l.HeightCm,
            MaxWeightKg = l.MaxWeightKg, Status = l.Status,
            Rules = rulesByZone[l.LocationId].Select(r => $"{r.Category.CategoryName} — {r.Brand?.BrandName ?? "Mọi hãng"}").ToList()
        }).ToList();
        var children = nodes.Where(n => n.ParentLocationId.HasValue).ToLookup(n => n.ParentLocationId!.Value);
        var zones = nodes.Where(n => n.LocationType == "ZONE" && n.ParentLocationId is null).ToList();
        var attached = zones.Select(n => n.LocationId).ToHashSet();
        var queue = new Queue<StorageLocationViewModel>(zones);
        while (queue.TryDequeue(out var parent))
        {
            foreach (var child in children[parent.LocationId])
            {
                // Only attach valid hierarchy edges. Bad parents/cycles remain visible as unlinked records.
                var valid = (parent.LocationType, parent.StorageType, child.LocationType) switch
                {
                    ("ZONE", _, "ROW") => true,
                    ("ROW", "RACK", "RACK") => true,
                    ("ROW", "FLOOR", "FLOOR_SLOT") => true,
                    ("RACK", _, "TIER") => true,
                    _ => false
                };
                if (!valid || !attached.Add(child.LocationId)) continue;
                if (child.LocationType == "TIER") child.ConditionPolicy = parent.ConditionPolicy;
                parent.Children.Add(child);
                queue.Enqueue(child);
            }
        }
        return new WarehouseDetailsViewModel
        {
            Warehouse = ToListItem(warehouse), Zones = zones,
            UnlinkedLocations = nodes.Where(n => !attached.Contains(n.LocationId)).ToList(),
            LocationCount = nodes.Count, RowCount = nodes.Count(n => n.LocationType == "ROW"),
            RackCount = nodes.Count(n => n.LocationType == "RACK"), TierCount = nodes.Count(n => n.LocationType == "TIER"),
            FloorSlotCount = nodes.Count(n => n.LocationType == "FLOOR_SLOT"),
            RackVolumeM3 = nodes.Sum(n => n.VolumeM3 ?? 0), FloorAreaM2 = nodes.Sum(n => n.FloorAreaM2 ?? 0),
            HasStoredItems = await warehouses.HasStockAsync(id, null, cancellationToken),
            SetupIssues = GetSetupIssues(locations, rules, attached)
        };
    }

    public async Task<WarehouseFormViewModel?> GetEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var w = await warehouses.FindByIdAsync(id, cancellationToken);
        return w is null ? null : new WarehouseFormViewModel
        {
            WarehouseId = w.WarehouseId, WarehouseName = w.WarehouseName, Address = w.Address,
            WarehouseType = w.WarehouseType, Status = w.Status,
            RentalStartDate = w.RentalStartDate, RentalEndDate = w.RentalEndDate,
            RowVersion = Convert.ToBase64String(w.RowVersion),
            IsOccupied = await warehouses.HasStockAsync(id, null, cancellationToken),
            IsLocked = (await warehouses.GetLockedScopeRootsAsync(id, cancellationToken)).Count > 0
        };
    }

    public async Task<WarehouseSaveResult> CreateAsync(WarehouseFormViewModel model, CancellationToken cancellationToken = default)
    {
        var errors = await ValidateFormAsync(model, null, cancellationToken);
        if (errors.Count > 0) return new() { Errors = errors };
        var warehouse = new WarehouseEntity { RowVersion = [] };
        ApplyForm(warehouse, model);
        try { await warehouses.AddAsync(warehouse, cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return DuplicateName(); }
        return new() { WarehouseId = warehouse.WarehouseId };
    }

    public async Task<WarehouseSaveResult> UpdateAsync(int id, WarehouseFormViewModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            return await warehouses.InTransactionAsync(id, async ct =>
            {
                var warehouse = await warehouses.FindByIdAsync(id, ct);
                if (warehouse is null) return new WarehouseSaveResult { NotFound = true };
                if (await warehouses.HasStockAsync(id, null, ct))
                    return Problem("Kho đang chứa hàng, kể cả hàng chờ xếp vị trí. Chỉ được sửa thông tin kho khi toàn bộ kho đã hết hàng.", conflict: true);
                if ((await warehouses.GetLockedScopeRootsAsync(id, ct)).Count > 0)
                    return Problem("Kho đang có phạm vi kiểm kê bị khóa. Chưa thể sửa thông tin kho.", conflict: true);
                var errors = await ValidateFormAsync(model, id, ct);
                var rowVersion = ParseRowVersion(model.RowVersion);
                if (rowVersion is null || !warehouse.RowVersion.SequenceEqual(rowVersion))
                    return StaleData();
                if (errors.Count > 0) return new WarehouseSaveResult { Errors = errors };
                ApplyForm(warehouse, model);
                await warehouses.UpdateAsync(warehouse, rowVersion, ct);
                return new WarehouseSaveResult { WarehouseId = warehouse.WarehouseId };
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleData();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return DuplicateName(); }
    }

    public async Task<StorageLocationFormViewModel?> GetLocationFormAsync(int warehouseId, int? locationId,
        int? parentId, CancellationToken cancellationToken = default)
    {
        var warehouse = await warehouses.FindByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null) return null;
        var locations = await warehouses.GetLocationsAsync(warehouseId, cancellationToken);
        StorageLocationFormViewModel model;
        if (locationId.HasValue)
        {
            var location = locations.SingleOrDefault(l => l.LocationId == locationId);
            if (location is null) return null;
            model = ToLocationForm(location);
            if (location.LocationType == "ZONE")
                model.Rules = (await warehouses.GetZoneRulesAsync(warehouseId, cancellationToken))
                    .Where(r => r.ZoneLocationId == locationId)
                    .Select(r => new ZoneRuleFormViewModel { CategoryId = r.CategoryId, BrandId = r.BrandId }).ToList();
        }
        else
        {
            var parent = locations.SingleOrDefault(l => l.LocationId == parentId);
            var type = parentId.HasValue ? ChildType(parent) : "ZONE";
            if (type is null) return null;
            model = new StorageLocationFormViewModel
            {
                WarehouseId = warehouseId, ParentLocationId = parentId, LocationType = type,
                ZoneType = type == "ZONE" ? "RESERVE" : null,
                StorageType = type == "ROW" ? "RACK" : null,
                ConditionPolicy = type is "RACK" or "FLOOR_SLOT" ? "GOOD" : null
            };
        }
        await PopulateLocationOptionsAsync(model, cancellationToken);
        return model;
    }

    public async Task PopulateLocationOptionsAsync(StorageLocationFormViewModel model, CancellationToken cancellationToken = default)
    {
        var warehouse = await warehouses.FindByIdAsync(model.WarehouseId, cancellationToken);
        var locations = await warehouses.GetLocationsAsync(model.WarehouseId, cancellationToken);
        var current = locations.SingleOrDefault(l => l.LocationId == model.LocationId);
        var parentId = current?.ParentLocationId ?? model.ParentLocationId;
        var parent = locations.SingleOrDefault(l => l.LocationId == parentId);
        model.WarehouseName = warehouse?.WarehouseName ?? "";
        model.ParentName = parent?.LocationName;
        model.ParentConditionLabel = parent?.ConditionPolicy switch { "GOOD" => "Hàng tốt", "DAMAGED" => "Hàng hỏng", _ => null };
        model.HasChildren = current is not null && locations.Any(l => l.ParentLocationId == current.LocationId);
        var scopeId = current?.LocationId ?? parentId;
        model.IsOccupied = scopeId.HasValue && await warehouses.HasStockAsync(model.WarehouseId,
            SubtreeIds(locations, scopeId.Value), cancellationToken);
        model.IsLocked = IsScopeLocked(locations, await warehouses.GetLockedScopeRootsAsync(model.WarehouseId, cancellationToken),
            scopeId, includeDescendants: current is not null);
        model.Categories = (await warehouses.GetCategoriesAsync(cancellationToken))
            .Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString())).ToList();
        model.Brands = (await warehouses.GetBrandsAsync(cancellationToken))
            .Select(b => new SelectListItem(b.BrandName + (b.Status == "ACTIVE" ? "" : " (ngừng hoạt động)"), b.BrandId.ToString())).ToList();
    }

    public async Task<WarehouseSaveResult> SaveLocationAsync(int warehouseId, int? locationId,
        StorageLocationFormViewModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            return await warehouses.InTransactionAsync(warehouseId, async ct =>
            {
                if (await warehouses.FindByIdAsync(warehouseId, ct) is null)
                    return new WarehouseSaveResult { NotFound = true };
                var locations = await warehouses.GetLocationsAsync(warehouseId, ct);
                var current = locationId.HasValue ? locations.SingleOrDefault(l => l.LocationId == locationId) : null;
                if (locationId.HasValue && current is null) return new WarehouseSaveResult { NotFound = true };
                if (current is not null && (current.ParentLocationId != model.ParentLocationId || current.LocationType != model.LocationType))
                    return Problem("Không được đổi vị trí cha hoặc cấp của vị trí đã tạo. Hãy tải lại cấu hình.");
                var parent = locations.SingleOrDefault(l => l.LocationId == model.ParentLocationId);
                var expectedType = model.ParentLocationId.HasValue ? ChildType(parent) : "ZONE";
                if (expectedType is null || expectedType != model.LocationType)
                    return Problem("Vị trí cha không tồn tại trong kho hoặc không phù hợp với cấp vị trí.");
                if (!HasValidAncestry(locations, parent))
                    return Problem("Nhánh vị trí cha không hợp lệ. Cần xử lý cấu trúc trước khi lưu.");
                if ((current is null || model.Status == "ACTIVE") && Ancestors(locations, parent?.LocationId).Any(l => l.Status != "ACTIVE"))
                    return Problem("Cần kích hoạt các vị trí cha trước khi thêm hoặc kích hoạt vị trí con.");

                // The full descendant set is used, even for a rename or an increase in capacity.
                // Adding a child also changes its parent's structure, so that parent branch must be empty.
                var scopeId = current?.LocationId ?? parent?.LocationId;
                var scopeIds = scopeId.HasValue ? SubtreeIds(locations, scopeId.Value) : [];
                if (scopeId.HasValue && await warehouses.HasStockAsync(warehouseId, scopeIds, ct))
                    return Problem("Vị trí hoặc nhánh cha đang chứa hàng. Chỉ được sửa cấu hình hoặc thêm vị trí con khi phạm vi bị ảnh hưởng đã hết hàng.", conflict: true);
                if (IsScopeLocked(locations, await warehouses.GetLockedScopeRootsAsync(warehouseId, ct), scopeId, current is not null))
                    return Problem("Phạm vi này đang bị khóa kiểm kê. Chưa thể thay đổi cấu trúc hoặc quy tắc.", conflict: true);

                var rowVersion = current is null ? null : ParseRowVersion(model.RowVersion);
                if (current is not null && (rowVersion is null || !current.RowVersion.SequenceEqual(rowVersion)))
                    return StaleData();
                var errors = ValidateLocation(model);
                if (current?.LocationType == "ROW" && current.StorageType != model.StorageType
                    && locations.Any(l => l.ParentLocationId == current.LocationId))
                    errors[nameof(model.StorageType)] = "Dãy đã có vị trí con nên không thể đổi giữa kệ và sàn. Hãy ngừng sử dụng nhánh cũ khi hết hàng rồi tạo dãy mới.";
                if (errors.Count == 0 && await warehouses.LocationNameExistsAsync(warehouseId, model.ParentLocationId,
                    model.LocationName.Trim(), locationId, ct))
                    errors[nameof(model.LocationName)] = "Tên vị trí đã tồn tại trong cùng cấp cha, kể cả vị trí ngừng hoạt động.";

                var rules = new List<ZoneStorageRule>();
                if (model.LocationType == "ZONE" && model.ZoneType == "STANDARD")
                {
                    var categories = (await warehouses.GetCategoriesAsync(ct)).Select(c => c.CategoryId).ToHashSet();
                    var brands = (await warehouses.GetBrandsAsync(ct)).ToDictionary(b => b.BrandId);
                    var existingPairs = current is null ? [] : (await warehouses.GetZoneRulesAsync(warehouseId, ct))
                        .Where(r => r.ZoneLocationId == current.LocationId).Select(r => (r.CategoryId, r.BrandId)).ToHashSet();
                    var pairs = new HashSet<(int, int?)>();
                    for (var i = 0; i < model.Rules.Count; i++)
                    {
                        var rule = model.Rules[i];
                        if (!categories.Contains(rule.CategoryId))
                            errors[$"Rules[{i}].CategoryId"] = "Loại hàng không tồn tại.";
                        if (rule.BrandId.HasValue && (!brands.TryGetValue(rule.BrandId.Value, out var brand)
                            || (brand.Status != "ACTIVE" && !existingPairs.Contains((rule.CategoryId, rule.BrandId)))))
                            errors[$"Rules[{i}].BrandId"] = "Hãng không tồn tại hoặc đã ngừng hoạt động; không thể gán mới.";
                        if (!pairs.Add((rule.CategoryId, rule.BrandId)))
                            errors[nameof(model.Rules)] = "Các cặp loại hàng và hãng không được trùng nhau.";
                        rules.Add(new ZoneStorageRule { CategoryId = rule.CategoryId, BrandId = rule.BrandId });
                    }
                }
                if (errors.Count > 0) return new WarehouseSaveResult { Errors = errors };

                var leaf = model.LocationType is "TIER" or "FLOOR_SLOT";
                var location = new StorageLocation
                {
                    LocationId = current?.LocationId ?? 0, WarehouseId = warehouseId,
                    ParentLocationId = model.ParentLocationId, LocationType = model.LocationType,
                    LocationName = model.LocationName.Trim(), Status = model.Status,
                    ZoneType = model.LocationType == "ZONE" ? model.ZoneType : null,
                    StorageType = model.LocationType == "ROW" ? model.StorageType : null,
                    ConditionPolicy = model.LocationType is "RACK" or "FLOOR_SLOT" ? model.ConditionPolicy : null,
                    CapacityMode = model.LocationType switch { "TIER" => "VOLUME", "FLOOR_SLOT" => "FLOOR_AREA", _ => null },
                    LengthCm = leaf ? model.LengthCm : null, WidthCm = leaf ? model.WidthCm : null,
                    HeightCm = leaf ? model.HeightCm : null, MaxWeightKg = leaf ? model.MaxWeightKg : null,
                    RowVersion = current?.RowVersion ?? []
                };
                await warehouses.SaveLocationAsync(location, rowVersion, rules,
                    current is not null && model.Status == "INACTIVE" ? scopeIds : [], ct);
                return new WarehouseSaveResult { WarehouseId = warehouseId, LocationId = location.LocationId };
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return StaleData(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return Problem("Tên vị trí hoặc cặp loại hàng/hãng đã tồn tại. Vui lòng tải lại cấu hình."); }
    }

    private static string? ChildType(StorageLocation? parent) => (parent?.LocationType, parent?.StorageType) switch
    {
        ("ZONE", _) => "ROW", ("ROW", "RACK") => "RACK", ("ROW", "FLOOR") => "FLOOR_SLOT", ("RACK", _) => "TIER", _ => null
    };

    private static HashSet<int> SubtreeIds(List<StorageLocation> locations, int rootId)
    {
        var children = locations.Where(l => l.ParentLocationId.HasValue).ToLookup(l => l.ParentLocationId!.Value);
        var ids = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        while (queue.TryDequeue(out var id))
            foreach (var child in children[id])
                if (ids.Add(child.LocationId)) queue.Enqueue(child.LocationId);
        return ids;
    }

    private static IEnumerable<StorageLocation> Ancestors(List<StorageLocation> locations, int? locationId)
    {
        var byId = locations.ToDictionary(l => l.LocationId);
        var visited = new HashSet<int>();
        while (locationId.HasValue && visited.Add(locationId.Value) && byId.TryGetValue(locationId.Value, out var location))
        {
            yield return location;
            locationId = location.ParentLocationId;
        }
    }

    private static bool HasValidAncestry(List<StorageLocation> locations, StorageLocation? parent)
    {
        if (parent is null) return true;
        var chain = Ancestors(locations, parent.LocationId).ToList();
        if (chain.Count == 0 || chain[^1].LocationType != "ZONE" || chain[^1].ParentLocationId.HasValue) return false;
        return chain.Zip(chain.Skip(1), (child, ancestor) => ChildType(ancestor) == child.LocationType).All(valid => valid);
    }

    private static bool IsScopeLocked(List<StorageLocation> locations, List<int?> roots, int? targetId, bool includeDescendants)
    {
        if (roots.Any(r => r is null)) return true;
        if (!targetId.HasValue) return false;
        var ancestors = Ancestors(locations, targetId).Select(l => l.LocationId).ToHashSet();
        var subtree = includeDescendants ? SubtreeIds(locations, targetId.Value) : [];
        return roots.Any(r => ancestors.Contains(r!.Value) || subtree.Contains(r.Value));
    }

    private static Dictionary<string, string> ValidateLocation(StorageLocationFormViewModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.First().ErrorMessage ?? "Dữ liệu không hợp lệ.");
    }

    private static List<string> GetSetupIssues(List<StorageLocation> locations, List<ZoneStorageRule> rules, HashSet<int> attached)
    {
        var issues = new List<string>();
        var active = locations.Where(l => l.Status == "ACTIVE").ToList();
        if (!active.Any(l => l.LocationType == "ZONE" && l.ParentLocationId is null))
            issues.Add("Cần ít nhất một Zone đang hoạt động.");
        if (attached.Count != locations.Count) issues.Add("Có vị trí chưa nối đúng cấu trúc kho.");
        foreach (var location in active)
        {
            var childType = ChildType(location);
            if (childType is not null && !active.Any(l => l.ParentLocationId == location.LocationId && l.LocationType == childType))
                issues.Add($"{location.LocationName}: chưa có vị trí con đang hoạt động.");
            var form = ToLocationForm(location);
            form.Rules = rules.Where(r => r.ZoneLocationId == location.LocationId)
                .Select(r => new ZoneRuleFormViewModel { CategoryId = r.CategoryId, BrandId = r.BrandId }).ToList();
            if (ValidateLocation(form).Count > 0) issues.Add($"{location.LocationName}: chưa đủ cấu hình loại, tình trạng hàng, quy tắc hoặc kích thước hợp lệ.");
            if (Ancestors(locations, location.ParentLocationId).Any(l => l.Status != "ACTIVE"))
                issues.Add($"{location.LocationName}: vị trí cha chưa hoạt động.");
        }
        return issues;
    }

    private static StorageLocationFormViewModel ToLocationForm(StorageLocation location) => new()
    {
        WarehouseId = location.WarehouseId, LocationId = location.LocationId, ParentLocationId = location.ParentLocationId,
        LocationName = location.LocationName, LocationType = location.LocationType, ZoneType = location.ZoneType,
        StorageType = location.StorageType, ConditionPolicy = location.ConditionPolicy, Status = location.Status,
        LengthCm = location.LengthCm, WidthCm = location.WidthCm, HeightCm = location.HeightCm, MaxWeightKg = location.MaxWeightKg,
        RowVersion = Convert.ToBase64String(location.RowVersion)
    };

    private static byte[]? ParseRowVersion(string? value)
    {
        var bytes = new byte[8];
        return value is not null && Convert.TryFromBase64String(value, bytes, out var written) && written == 8 ? bytes : null;
    }

    private static WarehouseSaveResult Problem(string message, string field = "", bool conflict = false) =>
        new() { Conflict = conflict, Errors = { [field] = message } };

    private static WarehouseSaveResult StaleData() => Problem(
        "Dữ liệu đã thay đổi hoặc phiên chỉnh sửa không hợp lệ. Hãy tải lại trang trước khi lưu tiếp.", conflict: true);

    private async Task<Dictionary<string, string>> ValidateFormAsync(WarehouseFormViewModel model, int? id, CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        var errors = results.GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.First().ErrorMessage ?? "Dữ liệu không hợp lệ.");
        if (errors.Count == 0 && await warehouses.NameExistsAsync(model.WarehouseName.Trim(), id, cancellationToken))
            errors[nameof(model.WarehouseName)] = "Tên kho đã tồn tại. Vui lòng chọn tên khác.";
        return errors;
    }

    private static WarehouseSaveResult DuplicateName() => new()
    {
        Errors = { [nameof(WarehouseFormViewModel.WarehouseName)] = "Tên kho đã tồn tại. Vui lòng chọn tên khác." }
    };

    private static void ApplyForm(WarehouseEntity warehouse, WarehouseFormViewModel model)
    {
        warehouse.WarehouseName = model.WarehouseName.Trim();
        warehouse.Address = model.Address.Trim();
        warehouse.WarehouseType = model.WarehouseType;
        warehouse.Status = model.Status;
        warehouse.RentalStartDate = model.WarehouseType == "RENTED" ? model.RentalStartDate : null;
        warehouse.RentalEndDate = model.WarehouseType == "RENTED" ? model.RentalEndDate : null;
    }

    private static WarehouseListItemViewModel ToListItem(WarehouseEntity w) => new()
    {
        WarehouseId = w.WarehouseId, WarehouseName = w.WarehouseName, Address = w.Address,
        WarehouseType = w.WarehouseType, RentalStartDate = w.RentalStartDate, RentalEndDate = w.RentalEndDate, Status = w.Status
    };
}
