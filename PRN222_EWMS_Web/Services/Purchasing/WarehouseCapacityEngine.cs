using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;

namespace PRN222_EWMS_Web.Services.Purchasing;

public class WarehouseCapacityEngine(EwmsDbContext db) : IWarehouseCapacityEngine
{
    public async Task<ProductCapacityResult> CalculateCapacityAsync(
        int warehouseId,
        int productId,
        int requestedQuantity,
        long? excludePoId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProductCapacityResult
        {
            ProductId = productId
        };

        // 1. Lấy thông tin kích thước và danh mục của sản phẩm
        var p = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

        if (p == null)
        {
            result.IsValid = false;
            result.ErrorMessage = "Không tìm thấy thông tin sản phẩm yêu cầu.";
            return result;
        }

        result.ProductCode = p.ProductCode;
        result.ProductName = p.ProductName;

        // Tính Footprint (diện tích chân đế m²) và Volume (thể tích m³)
        // Footprint = (LengthCm * WidthCm) / 10,000
        // Volume = (LengthCm * WidthCm * HeightCm) / 1,000,000
        decimal footprint = (p.LengthCm * p.WidthCm) / 10000m;
        decimal volume = (p.LengthCm * p.WidthCm * p.HeightCm) / 1000000m;
        decimal unitWeight = p.GrossWeightKg;

        // 2. Nạp toàn bộ vị trí lưu trữ của kho để xây dựng cây phân cấp (Zone -> Row -> Rack -> Tier/FloorSlot)
        var locations = await db.StorageLocations
            .AsNoTracking()
            .Where(l => l.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);

        var locMap = locations.ToDictionary(l => l.LocationId);

        // Hàm cục bộ tìm Root Zone
        StorageLocation? GetRootZone(StorageLocation loc)
        {
            var curr = loc;
            while (curr != null)
            {
                if (string.Equals(curr.LocationType, "ZONE", StringComparison.OrdinalIgnoreCase))
                    return curr;
                if (!curr.ParentLocationId.HasValue) return null;
                if (!locMap.TryGetValue(curr.ParentLocationId.Value, out curr!)) return null;
            }
            return null;
        }

        // Hàm cục bộ kiểm tra vị trí hoặc bất kỳ vị trí cha nào có thuộc khu lỗi/cách ly hay không
        bool IsDefectiveOrQuarantine(StorageLocation loc)
        {
            var curr = loc;
            while (curr != null)
            {
                if (string.Equals(curr.ZoneType, "DAMAGED", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(curr.ConditionPolicy, "DAMAGED", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(curr.StorageType, "QUARANTINE", StringComparison.OrdinalIgnoreCase)) return true;
                if (!curr.ParentLocationId.HasValue) break;
                if (!locMap.TryGetValue(curr.ParentLocationId.Value, out curr!)) break;
            }
            return false;
        }

        // 3. Lấy danh sách quy tắc phân khu ZoneStorageRule của kho
        var zoneRules = await db.ZoneStorageRules
            .AsNoTracking()
            .Where(r => r.ZoneLocation.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);

        // Xác định các Zone hợp lệ cho sản phẩm:
        // - ZoneType == 'RESERVE' (Khu dự phòng dùng chung khi Standard đầy)
        // - ZoneType == 'STANDARD' và khớp CategoryId (cùng BrandId nếu rule có BrandId)
        // - Loại trừ 100% DEFECTIVE và QUARANTINE
        var validZoneIds = new HashSet<int>();
        foreach (var loc in locations.Where(l => string.Equals(l.LocationType, "ZONE", StringComparison.OrdinalIgnoreCase)))
        {
            if (IsDefectiveOrQuarantine(loc)) continue;

            if (string.Equals(loc.ZoneType, "RESERVE", StringComparison.OrdinalIgnoreCase))
            {
                validZoneIds.Add(loc.LocationId);
            }
            else if (string.Equals(loc.ZoneType, "STANDARD", StringComparison.OrdinalIgnoreCase))
            {
                bool matchesRule = zoneRules.Any(r => r.ZoneLocationId == loc.LocationId
                                                   && r.CategoryId == p.CategoryId
                                                   && (r.BrandId == null || r.BrandId == p.BrandId));
                if (matchesRule)
                {
                    validZoneIds.Add(loc.LocationId);
                }
            }
        }

        if (validZoneIds.Count == 0)
        {
            result.HasValidZone = false;
            result.IsValid = false;
            result.ErrorMessage = $"Sản phẩm '{p.ProductName}' (Mã: {p.ProductCode}) không thuộc danh mục được phép lưu trữ tại kho tiếp nhận này hoặc kho chưa thiết lập khu vực lưu trữ phù hợp (Mã lỗi: MSG-015).";
            return result;
        }

        result.HasValidZone = true;

        // 4. Lấy dữ liệu sức chứa tính sẵn từ View CSDL vw_LocationCapacity
        var capacities = await db.VwLocationCapacities
            .AsNoTracking()
            .Where(c => c.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);

        int floorCapacity = 0;
        int rackCapacity = 0;

        foreach (var cap in capacities)
        {
            if (!locMap.TryGetValue(cap.LocationId, out var loc)) continue;
            if (!string.Equals(loc.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsDefectiveOrQuarantine(loc)) continue;

            var rootZone = GetRootZone(loc);
            if (rootZone == null || !validZoneIds.Contains(rootZone.LocationId)) continue;

            // 4.1 Tính sức chứa Ô sàn (FLOOR_SLOT)
            if (string.Equals(cap.LocationType, "FLOOR_SLOT", StringComparison.OrdinalIgnoreCase))
            {
                decimal remArea = Math.Max(0m, (cap.CapacityAreaM2 ?? 0m) - (cap.UsedAreaM2 ?? 0m));
                int floorQty = footprint > 0 ? (int)Math.Floor(remArea / footprint) : 0;

                // Nếu có giới hạn tải trọng sàn
                if (cap.MaxWeightKg.HasValue && cap.MaxWeightKg.Value > 0 && unitWeight > 0)
                {
                    decimal remWeight = Math.Max(0m, cap.MaxWeightKg.Value - cap.UsedWeightKg);
                    int wQty = (int)Math.Floor(remWeight / unitWeight);
                    floorQty = Math.Min(floorQty, wQty);
                }

                floorCapacity += floorQty;
            }
            // 4.2 Tính sức chứa Kệ nhiều tầng (TIER)
            else if (string.Equals(cap.LocationType, "TIER", StringComparison.OrdinalIgnoreCase))
            {
                decimal remVol = Math.Max(0m, (cap.CapacityVolumeM3 ?? 0m) - (cap.UsedVolumeM3 ?? 0m));
                int volQty = volume > 0 ? (int)Math.Floor(remVol / volume) : 0;

                if (cap.MaxWeightKg.HasValue && cap.MaxWeightKg.Value > 0 && unitWeight > 0)
                {
                    decimal remWeight = Math.Max(0m, cap.MaxWeightKg.Value - cap.UsedWeightKg);
                    int wQty = (int)Math.Floor(remWeight / unitWeight);
                    rackCapacity += Math.Min(volQty, wQty);
                }
                else
                {
                    rackCapacity += volQty;
                }
            }
        }

        result.FloorSlotCapacity = floorCapacity;
        result.RackTierCapacity = rackCapacity;

        // 5. Tính toán lượng hàng đang trên đường về (Incoming Pipeline)
        // 5.1 SupplySource INCOMING / IN_TRANSIT (PO khác sắp về, khách trả hàng, điều chuyển)
        var supplyQuery = db.SupplySources
            .AsNoTracking()
            .Where(s => s.ProductId == productId
                     && s.WarehouseId == warehouseId
                     && (s.SupplyState == "INCOMING" || s.SupplyState == "IN_TRANSIT")
                     && (s.ItemCondition == "GOOD" || s.ItemCondition == null));

        if (excludePoId.HasValue && excludePoId.Value > 0)
        {
            supplyQuery = supplyQuery.Where(s => s.Poline == null || s.Poline.PurchaseOrderId != excludePoId.Value);
        }

        int supplyPipeline = await supplyQuery.SumAsync(s => (int?)s.RemainingQuantity, cancellationToken) ?? 0;

        // 5.2 SerialItem chờ xếp vị trí (RECEIVED hoặc IN_WAREHOUSE nhưng CurrentLocationId == null)
        int awaitingPutaway = await db.SerialItems
            .AsNoTracking()
            .Where(s => s.ProductId == productId
                     && s.CurrentWarehouseId == warehouseId
                     && (s.StockState == "RECEIVED" || s.StockState == "IN_WAREHOUSE")
                     && s.CurrentLocationId == null
                     && (s.ItemCondition == "GOOD" || s.ItemCondition == null))
            .CountAsync(cancellationToken);

        result.SupplyPipelineQuantity = supplyPipeline;
        result.AwaitingPutawayQuantity = awaitingPutaway;

        // 6. Đối chiếu với số lượng yêu cầu đặt mua
        if (requestedQuantity > result.MaxOrderQuantity)
        {
            result.IsValid = false;
            result.ErrorMessage = $"Kệ/ô sàn không đủ sức chứa cho số lượng yêu cầu của sản phẩm '{p.ProductName}' (Mã: {p.ProductCode}). Số lượng đặt: {requestedQuantity}, Sức chứa tối đa còn lại: {result.MaxOrderQuantity} (Mã lỗi: MSG-016).";
        }
        else
        {
            result.IsValid = true;
        }

        return result;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidatePurchaseOrderLinesCapacityAsync(
        int warehouseId,
        IEnumerable<(int ProductId, int Quantity)> lines,
        long? excludePoId = null,
        CancellationToken cancellationToken = default)
    {
        var inputs = lines.ToList();
        if (inputs.Count == 0 || inputs.Any(l => l.Quantity <= 0)) return (false, "Cần sản phẩm và số lượng nguyên dương.");
        var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.ProductId, cancellationToken);
        if (inputs.Any(l => !products.ContainsKey(l.ProductId))) return (false, "Có sản phẩm không còn tồn tại.");
        CapacityProduct Profile(int id)
        {
            var p = products[id];
            return new(p.ProductId, p.ProductCode, p.CategoryId, p.BrandId, p.LengthCm, p.WidthCm, p.HeightCm, p.GrossWeightKg);
        }
        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(cancellationToken);
        var rules = await db.ZoneStorageRules.AsNoTracking().Where(r => r.ZoneLocation.WarehouseId == warehouseId).ToListAsync(cancellationToken);
        var serials = await db.SerialItems.AsNoTracking().Where(s => s.CurrentWarehouseId == warehouseId
                && (s.StockState == "IN_WAREHOUSE" || s.StockState == "RECEIVED"))
            .Select(s => new { s.ProductId, s.ItemCondition, s.CurrentLocationId }).ToListAsync(cancellationToken);
        var map = locations.ToDictionary(l => l.LocationId);
        var slots = new List<CapacitySlot>();
        foreach (var leaf in locations.Where(l => l.LocationType is "TIER" or "FLOOR_SLOT"))
        {
            var chain = new List<StorageLocation>();
            StorageLocation? node = leaf;
            var seen = new HashSet<int>();
            while (node != null && seen.Add(node.LocationId) && chain.Count < 5)
            {
                chain.Add(node);
                node = node.ParentLocationId is int parent && map.TryGetValue(parent, out var location) ? location : null;
            }
            bool floor = leaf.LocationType == "FLOOR_SLOT";
            if (chain.Any(l => l.Status != "ACTIVE") || chain[^1].ParentLocationId != null) continue;
            if (floor ? chain.Count != 3 || chain[1].LocationType != "ROW" || chain[1].StorageType != "FLOOR"
                : chain.Count != 4 || chain[1].LocationType != "RACK" || chain[2].LocationType != "ROW" || chain[2].StorageType != "RACK") continue;
            var zone = chain[^1];
            var condition = floor ? leaf.ConditionPolicy : chain[1].ConditionPolicy;
            if (zone.LocationType != "ZONE" || zone.ZoneType is not ("STANDARD" or "RESERVE") || condition is not ("GOOD" or "DAMAGED")
                || leaf.LengthCm is not > 0 || leaf.WidthCm is not > 0 || (!floor && leaf.HeightCm is not > 0)
                || leaf.CapacityMode != (floor ? "FLOOR_AREA" : "VOLUME")) continue;
            slots.Add(new CapacitySlot { Id = leaf.LocationId, Reserve = zone.ZoneType == "RESERVE", Floor = floor, Condition = condition,
                Length = leaf.LengthCm.Value, Width = leaf.WidthCm.Value, Height = leaf.HeightCm, MaxWeight = leaf.MaxWeightKg,
                Rules = rules.Where(r => r.ZoneLocationId == zone.LocationId).Select(r => (r.CategoryId,r.BrandId)).ToList(),
                Loads = serials.Where(s => s.CurrentLocationId == leaf.LocationId).GroupBy(s => new { s.ProductId,s.ItemCondition })
                    .Select(g => new CapacityLoad(Profile(g.Key.ProductId),g.Key.ItemCondition,g.LongCount())).ToList() });
        }
        var pending = serials.Where(s => s.CurrentLocationId == null).GroupBy(s => new { s.ProductId,s.ItemCondition })
            .Select(g => new CapacityLoad(Profile(g.Key.ProductId),g.Key.ItemCondition,g.LongCount())).ToList();
        // Count document remainders once. TRANSFER_INCOMING is a projection of these shipments, not extra stock.
        var purchases = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrder.WarehouseId == warehouseId
                && (excludePoId == null || l.PurchaseOrderId != excludePoId)
                && (l.PurchaseOrder.Status == "CONFIRMED" || l.PurchaseOrder.Status == "PARTIALLY_RECEIVED"))
            .Select(l => new { l.ProductId, Quantity = (long)l.OrderedQuantity-l.ClosedQuantity
                -db.InboundItems.LongCount(i => i.PolineId == l.PolineId && i.IsActive && i.Inbound.Status == "DONE") }).ToListAsync(cancellationToken);
        pending.AddRange(purchases.Select(l => new CapacityLoad(Profile(l.ProductId),"GOOD",Math.Max(0,l.Quantity))));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var transfers = await db.TransferOrderLines.AsNoTracking().Where(l => l.TransferOrder.ToWarehouseId == warehouseId
                && (l.TransferOrder.Status == "RELEASED" || l.TransferOrder.Status == "IN_PROGRESS")
                && (l.TransferOrder.ExpectedDate == null || l.TransferOrder.ExpectedDate >= today
                    || l.TransferOrder.TransferDispatches.Any() || l.TransferOrder.TransferReceipts.Any()))
            .Select(l => new { l.ProductId,l.TransferOrder.ItemCondition, Quantity = (long)l.RequestedQuantity-l.CancelledQuantity
                -db.TransferReceiptItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.TransferReceipt.Status == "DONE")
                -db.TransferLossRequestItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.LossRequest.AppliedAt != null) }).ToListAsync(cancellationToken);
        pending.AddRange(transfers.Select(l => new CapacityLoad(Profile(l.ProductId),l.ItemCondition,Math.Max(0,l.Quantity))));
        var returns = await db.CustomerReturnOrderLines.AsNoTracking().Where(l => l.ReturnOrder.WarehouseId == warehouseId
                && (l.ReturnOrder.Status == "OPEN" || l.ReturnOrder.Status == "PARTIALLY_RECEIVED"))
            .Select(l => new { l.ProductId, Quantity = (long)l.RequestedQuantity-l.CancelledQuantity
                -db.ReturnReceiptItems.LongCount(i => i.ReturnLineId == l.ReturnLineId && i.IsActive && i.ReturnReceipt.Status == "DONE") }).ToListAsync(cancellationToken);
        pending.AddRange(returns.Select(l => new CapacityLoad(Profile(l.ProductId),"GOOD",Math.Max(0,l.Quantity))));
        // One shared simulation for all products, so no two lines reuse the same remaining area/volume/weight.
        // Conservatively retain incoming capacity; do not promise space from an uncompleted outbound.
        var errors = TransferCapacityPlanner.Allocate(slots, pending.Concat(inputs.Select(l => new CapacityLoad(Profile(l.ProductId),"GOOD",l.Quantity))));
        return errors.Count == 0 ? (true, null) : (false, "Sức chứa toàn bộ PO và các chứng từ đã phát hành: " + string.Join(" ", errors));
    }
}
