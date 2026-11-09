using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

internal static class PutawayRules
{
    public static string? SerialUnavailable(SerialItem item, PutawayLayout layout, bool claimed)
    {
        if (item.CurrentLocationId.HasValue) return "Serial đã được xếp kho. Putaway không đổi vị trí đã lưu.";
        if (item.StockState != "IN_WAREHOUSE") return "Serial không còn ở trạng thái đã nhập kho.";
        if (item.CurrentWarehouseId != layout.Warehouse.WarehouseId) return "Serial không thuộc kho được giao.";
        if (claimed) return "Serial đang được giữ để picking/xuất/chuyển kho. Chưa thể Putaway.";
        if (layout.AwaitingLocked) return "Hàng chờ xếp kho đang nằm trong phạm vi kiểm kê bị khóa.";
        if (layout.Warehouse.Status != "ACTIVE") return "Kho đã ngừng hoạt động.";
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (layout.Warehouse.WarehouseType == "RENTED" &&
            (layout.Warehouse.RentalStartDate is not DateOnly start || layout.Warehouse.RentalEndDate is not DateOnly end
                || today < start || today > end))
            return "Kho thuê chưa bắt đầu, đã hết hạn hoặc thiếu kỳ thuê. Cần cập nhật hợp đồng trước khi xếp kho.";
        if (item.ItemCondition is not ("GOOD" or "DAMAGED")) return "Tình trạng hàng chưa hợp lệ.";
        var source = item.CurrentSupplySource;
        if (source is null || source.SupplyState != "PHYSICAL" || source.WarehouseId != item.CurrentWarehouseId
            || source.ProductId != item.ProductId || source.ItemCondition != item.ItemCondition)
            return "Nguồn hàng hiện tại không khớp kho, sản phẩm hoặc tình trạng của serial.";
        var receiptDone = source.SourceType switch
        {
            "INBOUND" => source.Inbound?.Status == "DONE" && source.Inbound.PurchaseOrder.WarehouseId == item.CurrentWarehouseId,
            "RETURN" => source.ReturnReceipt?.Status == "DONE" && source.ReturnReceipt.ReturnOrder.WarehouseId == item.CurrentWarehouseId,
            _ => false
        };
        if (!receiptDone) return "Chỉ xếp hàng mua hoặc hàng khách trả đã được nhận chính thức bằng phiếu DONE.";
        if (item.Product.Status != "ACTIVE") return "Sản phẩm đã ngừng hoạt động. Cần kiểm tra lại hồ sơ sản phẩm.";
        if (!ValidProfile(item.Product.LengthCm, item.Product.WidthCm, item.Product.HeightCm, item.Product.GrossWeightKg))
            return "Sản phẩm thiếu kích thước đóng gói hoặc khối lượng hợp lệ.";
        return null;
    }

    public static (PutawayLocationViewModel? Location, string? Reason) Evaluate(SerialItem item, PutawayLayout layout,
        StorageLocation target, IReadOnlyCollection<PutawayOccupant> occupants)
    {
        (PutawayLocationViewModel?, string?) Fail(string message) => (null, message);
        if (target.LocationType is not ("TIER" or "FLOOR_SLOT")) return Fail("Chỉ được đặt tại tầng kệ hoặc ô sàn.");
        var byId = layout.Locations.ToDictionary(l => l.LocationId);
        var chain = new List<StorageLocation>();
        var seen = new HashSet<int>();
        StorageLocation? node = target;
        while (node is not null)
        {
            if (!seen.Add(node.LocationId) || chain.Count >= 4) return Fail("Cây vị trí sai cấp hoặc có chu trình.");
            if (node.WarehouseId != layout.Warehouse.WarehouseId || node.Status != "ACTIVE")
                return Fail("Vị trí hoặc một cấp cha đã ngừng hoạt động hoặc không thuộc kho.");
            if (layout.LockedLocations.Contains(node.LocationId)) return Fail("Vị trí thuộc phạm vi kiểm kê bị khóa.");
            chain.Add(node);
            if (node.ParentLocationId is not int parentId) break;
            if (!byId.TryGetValue(parentId, out node)) return Fail("Thiếu vị trí cha trong kho.");
        }
        var floor = target.LocationType == "FLOOR_SLOT";
        if (floor ? chain.Count != 3 || chain[1].LocationType != "ROW" || chain[1].StorageType != "FLOOR" || chain[2].LocationType != "ZONE"
            : chain.Count != 4 || chain[1].LocationType != "RACK" || chain[2].LocationType != "ROW" || chain[2].StorageType != "RACK" || chain[3].LocationType != "ZONE")
            return Fail("Cây phải là Zone → Dãy → Ô sàn hoặc Zone → Dãy → Kệ → Tầng.");
        var zone = chain[^1];
        if (zone.ParentLocationId.HasValue || zone.ZoneType is not ("STANDARD" or "RESERVE")) return Fail("Zone chưa được cấu hình hợp lệ.");
        var condition = floor ? target.ConditionPolicy : chain[1].ConditionPolicy;
        if (condition != item.ItemCondition) return Fail("Tình trạng hàng không khớp khu GOOD/DAMAGED.");
        if (zone.ZoneType == "STANDARD" && !layout.Rules.Any(r => r.ZoneLocationId == zone.LocationId
            && r.CategoryId == item.Product.CategoryId && (r.BrandId is null || r.BrandId == item.Product.BrandId)))
            return Fail("Zone không nhận cặp loại hàng và hãng của sản phẩm.");
        if (target.CapacityMode != (floor ? "FLOOR_AREA" : "VOLUME") || target.LengthCm is not > 0 || target.WidthCm is not > 0
            || (!floor && target.HeightCm is not > 0) || target.HeightCm is <= 0 || target.MaxWeightKg is <= 0)
            return Fail("Vị trí thiếu kích thước, chế độ sức chứa hoặc tải trọng hợp lệ.");
        var current = occupants.Where(o => o.LocationId == target.LocationId && o.SerialId != item.SerialItemId).ToList();
        if (current.Any(o => o.WarehouseId != layout.Warehouse.WarehouseId || o.Condition != item.ItemCondition
            || !ValidProfile(o.LengthCm, o.WidthCm, o.HeightCm, o.WeightKg)))
            return Fail("Hàng đang đặt có hồ sơ không hợp lệ hoặc không khớp kho/tình trạng; chưa thể tính chỗ trống an toàn.");
        var incoming = new PutawayOccupant(item.SerialItemId, layout.Warehouse.WarehouseId, target.LocationId,
            item.ItemCondition, item.Product.LengthCm, item.Product.WidthCm, item.Product.HeightCm, item.Product.GrossWeightKg);
        var all = current.Append(incoming).ToList();
        if (all.Any(p => p.LengthCm > target.LengthCm || p.WidthCm > target.WidthCm
            || (target.HeightCm.HasValue && p.HeightCm > target.HeightCm)))
            return Fail("Kiện vượt kích thước sử dụng theo hướng đóng gói cố định.");
        var usedWeight = current.Sum(o => o.WeightKg);
        if (target.MaxWeightKg.HasValue && usedWeight + incoming.WeightKg > target.MaxWeightKg)
            return Fail("Vị trí không còn đủ tải trọng.");
        var total = target.LengthCm.Value * target.WidthCm.Value / 10_000m;
        var used = current.Sum(o => o.LengthCm * o.WidthCm / 10_000m);
        if (floor)
        {
            if (used + incoming.LengthCm * incoming.WidthCm / 10_000m > total) return Fail("Ô sàn không đủ diện tích còn lại.");
            if (!CanPackFloor(target.LengthCm.Value, target.WidthCm.Value, all))
                return Fail("Chưa bố trí vừa các kiện cạnh nhau trên ô sàn, dù tổng diện tích có thể còn đủ.");
        }
        else
        {
            total = target.LengthCm.Value * target.WidthCm.Value * target.HeightCm!.Value / 1_000_000m;
            used = current.Sum(o => o.LengthCm * o.WidthCm * o.HeightCm / 1_000_000m);
            if (used + incoming.LengthCm * incoming.WidthCm * incoming.HeightCm / 1_000_000m > total)
                return Fail("Tầng kệ không đủ thể tích còn lại.");
        }
        return (new PutawayLocationViewModel
        {
            Id = target.LocationId, Path = Path(layout, target.LocationId), ZoneId = zone.LocationId, Zone = zone.LocationName,
            RowId = chain[floor ? 1 : 2].LocationId, Row = chain[floor ? 1 : 2].LocationName,
            RackId = floor ? null : chain[1].LocationId, Rack = floor ? null : chain[1].LocationName,
            Name = target.LocationName, IsReserve = zone.ZoneType == "RESERVE", IsFloor = floor,
            LengthCm = target.LengthCm.Value, WidthCm = target.WidthCm.Value, HeightCm = target.HeightCm,
            TotalCapacity = total, UsedCapacity = used, UsedWeightKg = usedWeight,
            MaxWeightKg = target.MaxWeightKg, OccupantCount = current.Count
        }, null);
    }

    public static string Path(PutawayLayout layout, int locationId)
    {
        var byId = layout.Locations.ToDictionary(l => l.LocationId);
        var names = new List<string>();
        var seen = new HashSet<int>();
        int? id = locationId;
        while (id.HasValue && seen.Add(id.Value) && byId.TryGetValue(id.Value, out var location))
        {
            names.Add(location.LocationName);
            id = location.ParentLocationId;
        }
        names.Reverse();
        return layout.Warehouse.WarehouseName + " → " + (names.Count > 0 ? string.Join(" → ", names) : $"Vị trí #{locationId}");
    }

    private static bool ValidProfile(decimal length, decimal width, decimal height, decimal weight) => length > 0 && width > 0 && height > 0 && weight > 0;

    private static bool CanPackFloor(decimal length, decimal width, List<PutawayOccupant> packages)
    {
        // Conservative 2D shelves: no rotation/stacking and no new x/y columns in the existing schema.
        var shelves = new List<(decimal Width, decimal UsedLength)>();
        var usedWidth = 0m;
        foreach (var package in packages.OrderByDescending(p => p.WidthCm).ThenByDescending(p => p.LengthCm).ThenBy(p => p.SerialId))
        {
            var index = shelves.FindIndex(s => package.WidthCm <= s.Width && s.UsedLength + package.LengthCm <= length);
            if (index >= 0) shelves[index] = (shelves[index].Width, shelves[index].UsedLength + package.LengthCm);
            else
            {
                if (package.LengthCm > length || usedWidth + package.WidthCm > width) return false;
                shelves.Add((package.WidthCm, package.LengthCm));
                usedWidth += package.WidthCm;
            }
        }
        return true;
    }
}
