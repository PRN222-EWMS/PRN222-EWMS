using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class TransferCapacityService(EwmsDbContext db, TimeProvider clock)
{
    public async Task EvaluateAsync(CreateTransferOrderViewModel model, TransferAssessment result, long? excludeOrderId = null)
    {
        var today = TransferPolicy.Today(clock);
        var horizon = model.ExpectedDate ?? today;
        var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.ProductId);
        CapacityProduct Profile(int id)
        {
            var p = products[id];
            return new(p.ProductId, p.ProductCode, p.CategoryId, p.BrandId, p.LengthCm, p.WidthCm, p.HeightCm, p.GrossWeightKg);
        }
        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == model.ToWarehouseId).ToListAsync();
        var rules = await db.ZoneStorageRules.AsNoTracking().Where(r => r.ZoneLocation.WarehouseId == model.ToWarehouseId).ToListAsync();
        var map = locations.ToDictionary(l => l.LocationId);
        var serials = await db.SerialItems.AsNoTracking().Where(s => s.CurrentWarehouseId == model.ToWarehouseId
            && (s.StockState == "IN_WAREHOUSE" || s.StockState == "RECEIVED"))
            .Select(s => new { s.SerialItemId, s.ProductId, s.ItemCondition, s.CurrentLocationId, s.CurrentSupplySourceId, s.IsQuarantined }).ToListAsync();

        List<CapacitySlot> Slots(HashSet<long> leaving)
        {
            var slots = new List<CapacitySlot>();
            foreach (var leaf in locations.Where(l => l.LocationType is "TIER" or "FLOOR_SLOT"))
            {
                var chain = new List<StorageLocation>();
                StorageLocation? node = leaf;
                var seen = new HashSet<int>();
                while (node != null && seen.Add(node.LocationId) && chain.Count < 5)
                {
                    chain.Add(node);
                    node = node.ParentLocationId is int id && map.TryGetValue(id, out var parent) ? parent : null;
                }
                bool floor = leaf.LocationType == "FLOOR_SLOT";
                if (chain.Any(l => l.Status != "ACTIVE") || chain[^1].ParentLocationId != null) continue;
                if (floor ? chain.Count != 3 || chain[1].LocationType != "ROW" || chain[1].StorageType != "FLOOR"
                    : chain.Count != 4 || chain[1].LocationType != "RACK" || chain[2].LocationType != "ROW" || chain[2].StorageType != "RACK") continue;
                var zone = chain[^1];
                var condition = floor ? leaf.ConditionPolicy : chain[1].ConditionPolicy;
                if (zone.LocationType != "ZONE" || zone.ZoneType is not ("STANDARD" or "RESERVE")
                    || condition is not ("GOOD" or "DAMAGED") || leaf.LengthCm is not > 0 || leaf.WidthCm is not > 0
                    || (!floor && leaf.HeightCm is not > 0) || leaf.CapacityMode != (floor ? "FLOOR_AREA" : "VOLUME")) continue;
                slots.Add(new CapacitySlot
                {
                    Id = leaf.LocationId, Reserve = zone.ZoneType == "RESERVE", Floor = floor, Condition = condition,
                    Length = leaf.LengthCm.Value, Width = leaf.WidthCm.Value, Height = leaf.HeightCm, MaxWeight = leaf.MaxWeightKg,
                    Rules = rules.Where(r => r.ZoneLocationId == zone.LocationId).Select(r => (r.CategoryId, r.BrandId)).ToList(),
                    Loads = serials.Where(s => s.CurrentLocationId == leaf.LocationId && !leaving.Contains(s.SerialItemId))
                        .GroupBy(s => new { s.ProductId, s.ItemCondition }).Select(g => new CapacityLoad(Profile(g.Key.ProductId), g.Key.ItemCondition, g.LongCount())).ToList()
                });
            }
            return slots;
        }
        var demand = model.Lines.Select(l => new CapacityLoad(Profile(l.ProductId), model.ItemCondition, l.Quantity)).ToList();
        var awaiting = serials.Where(s => s.CurrentLocationId == null).GroupBy(s => new { s.ProductId, s.ItemCondition })
            .Select(g => new CapacityLoad(Profile(g.Key.ProductId), g.Key.ItemCondition, g.LongCount())).ToList();
        var currentSlots = Slots([]);
        var currentErrors = TransferCapacityPlanner.Allocate(currentSlots, awaiting.Concat(demand));
        result.CurrentCapacity = currentErrors.Count == 0 ? "Hiện tại: đủ chỗ cho toàn bộ lệnh và hàng chờ xếp kho."
            : "Hiện tại: " + string.Join(" ", currentErrors);
        if (currentErrors.Count > 0) result.Warnings.Add(result.CurrentCapacity);

        // Credit only dated, released outbound commitments backed by physical stock.
        // Undated/unreserved demand must not create fictitious free space.
        var outgoing = await db.Reservations.AsNoTracking().Where(r => r.WarehouseId == model.ToWarehouseId
            && r.Status == "ACTIVE" && r.SupplyState == "PHYSICAL" && (excludeOrderId == null || r.TransferOrderId != excludeOrderId)
            && ((r.SalesOrder != null && (r.SalesOrder.Status == "SUBMITTED" || r.SalesOrder.Status == "PARTIALLY_FULFILLED")
                    && r.SalesOrder.RequestedDate != null && r.SalesOrder.RequestedDate <= horizon)
                || (r.TransferOrder != null && (r.TransferOrder.Status == "RELEASED" || r.TransferOrder.Status == "IN_PROGRESS")
                    && r.TransferOrder.DispatchDate != null && r.TransferOrder.DispatchDate <= horizon
                    && (r.TransferOrder.ExpectedDate >= today || r.TransferOrder.ExpectedDate == null
                        || r.TransferOrder.TransferDispatches.Any() || r.TransferOrder.TransferReceipts.Any()))))
            .GroupBy(r => r.SupplySourceId).Select(g => new { SourceId = g.Key,
                Qty = g.Sum(r => (long)r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity) }).ToListAsync();
        var leaving = new HashSet<long>();
        foreach (var o in outgoing)
        {
            long left = o.Qty;
            // Free awaiting space first; this avoids assuming a more favourable future picking location.
            foreach (var s in serials.Where(s => s.CurrentSupplySourceId == o.SourceId && !s.IsQuarantined)
                .OrderBy(s => s.CurrentLocationId.HasValue).ThenBy(s => s.SerialItemId))
            {
                if (left-- <= 0) break;
                leaving.Add(s.SerialItemId);
            }
        }
        var arrivals = new List<CapacityLoad>();
        var po = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrder.WarehouseId == model.ToWarehouseId
            && (l.PurchaseOrder.Status == "CONFIRMED" || l.PurchaseOrder.Status == "PARTIALLY_RECEIVED")
            && l.PurchaseOrder.ExpectedArrivalDate <= horizon)
            .Select(l => new { l.ProductId, Qty = (long)l.OrderedQuantity - l.ClosedQuantity
                - db.InboundItems.LongCount(i => i.PolineId == l.PolineId && i.IsActive && i.Inbound.Status == "DONE") }).ToListAsync();
        arrivals.AddRange(po.Select(l => new CapacityLoad(Profile(l.ProductId), "GOOD", Math.Max(0, l.Qty))));
        var transfers = await db.TransferOrderLines.AsNoTracking().Where(l => l.TransferOrder.ToWarehouseId == model.ToWarehouseId
            && (excludeOrderId == null || l.TransferOrderId != excludeOrderId)
            && (l.TransferOrder.Status == "RELEASED" || l.TransferOrder.Status == "IN_PROGRESS")
            && (l.TransferOrder.ExpectedDate == null || l.TransferOrder.ExpectedDate <= horizon)
            && (l.TransferOrder.ExpectedDate == null || l.TransferOrder.ExpectedDate >= today
                || l.TransferOrder.TransferDispatches.Any() || l.TransferOrder.TransferReceipts.Any()))
            .Select(l => new { l.ProductId, l.TransferOrder.ItemCondition, Qty = (long)l.RequestedQuantity - l.CancelledQuantity
                - db.TransferReceiptItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.TransferReceipt.Status == "DONE")
                - db.TransferLossRequestItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.LossRequest.AppliedAt != null) }).ToListAsync();
        arrivals.AddRange(transfers.Select(l => new CapacityLoad(Profile(l.ProductId), l.ItemCondition, Math.Max(0, l.Qty))));
        // Return orders have no ETA/expected-condition field. Their issued remainder is held conservatively now.
        var returns = await db.CustomerReturnOrderLines.AsNoTracking().Where(l => l.ReturnOrder.WarehouseId == model.ToWarehouseId
            && (l.ReturnOrder.Status == "OPEN" || l.ReturnOrder.Status == "PARTIALLY_RECEIVED"))
            .Select(l => new { l.ProductId, Qty = (long)l.RequestedQuantity - l.CancelledQuantity
                - db.ReturnReceiptItems.LongCount(i => i.ReturnLineId == l.ReturnLineId && i.IsActive && i.ReturnReceipt.Status == "DONE") }).ToListAsync();
        arrivals.AddRange(returns.Select(l => new CapacityLoad(Profile(l.ProductId), "GOOD", Math.Max(0, l.Qty))));
        if (returns.Any(l => l.Qty > 0)) result.Warnings.Add("Có lệnh khách trả chưa nhận và chưa có ETA/tình trạng dự kiến; dự báo đang dự phòng phần còn lại ở khu GOOD.");
        var futureAwaiting = serials.Where(s => s.CurrentLocationId == null && !leaving.Contains(s.SerialItemId))
            .GroupBy(s => new { s.ProductId, s.ItemCondition }).Select(g => new CapacityLoad(Profile(g.Key.ProductId), g.Key.ItemCondition, g.LongCount()));
        var forecastSlots = Slots(leaving);
        var forecastErrors = TransferCapacityPlanner.Allocate(forecastSlots, futureAwaiting.Concat(arrivals).Concat(demand));
        result.ForecastCapacity = $"Dự báo đến {horizon:dd/MM/yyyy}: " + (forecastErrors.Count == 0 ? "đủ chỗ cho toàn bộ lệnh." : string.Join(" ", forecastErrors));
        if (forecastErrors.Count > 0) result.Errors.Add(result.ForecastCapacity);
        if (model.ExpectedDate == null) result.Warnings.Add("Chưa có ngày dự kiến đến; dự báo tạm tính tại hôm nay. Cần kiểm tra lại khi phát hành.");
    }
}
