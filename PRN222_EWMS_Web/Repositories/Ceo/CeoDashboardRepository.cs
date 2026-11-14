using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Repositories.Ceo;

public class CeoDashboardRepository(EwmsDbContext db) : ICeoDashboardRepository
{
    private static readonly string[] ActiveStocktakeStatuses = ["COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL"];
    private static readonly string[] OpenOutboundStatuses = ["DRAFT", "READY", "PICKING"];

    public async Task<CeoDashboardViewModel> GetDashboardAsync(CeoDashboardFilter filter,
        DateTime fromUtc, DateTime toUtc, DateOnly today)
    {
        var allWarehouses = await db.Warehouses.AsNoTracking().OrderBy(w => w.WarehouseName)
            .Select(w => new CeoDashboardWarehouse
            {
                Id = w.WarehouseId, Name = w.WarehouseName, Type = w.WarehouseType, Status = w.Status,
                RentalEndDate = w.RentalEndDate,
                IsOperational = w.Status == "ACTIVE" && (w.WarehouseType != "RENTED"
                    || (w.RentalStartDate.HasValue && w.RentalEndDate.HasValue
                        && w.RentalStartDate <= today && w.RentalEndDate >= today))
            }).ToListAsync();
        var model = new CeoDashboardViewModel
        {
            Filter = filter,
            WarehouseOptions = allWarehouses.Select(w => new CeoDashboardWarehouseOption(w.Id, w.Name)).ToList(),
            Warehouses = allWarehouses.Where(w => !filter.WarehouseId.HasValue || w.Id == filter.WarehouseId.Value).ToList()
        };
        if (filter.WarehouseId.HasValue && model.Warehouses.Count == 0)
        {
            model.Error = "Kho được chọn không tồn tại. Vui lòng chọn lại kho.";
            return model;
        }
        foreach (var warehouse in model.Warehouses)
            warehouse.LeaseDaysLeft = warehouse.Type == "RENTED" && warehouse.RentalEndDate.HasValue
                ? warehouse.RentalEndDate.Value.DayNumber - today.DayNumber : null;
        var byWarehouse = model.Warehouses.ToDictionary(w => w.Id);
        var ids = byWarehouse.Keys.ToArray();
        if (ids.Length == 0) return model;

        var stock = db.SerialItems.AsNoTracking().Where(s => s.StockState == "IN_WAREHOUSE"
            && s.CurrentWarehouseId.HasValue && ids.Contains(s.CurrentWarehouseId.Value));
        var inventory = await stock.GroupBy(s => s.CurrentWarehouseId!.Value).Select(g => new
        {
            WarehouseId = g.Key, Count = g.LongCount(), Good = g.LongCount(s => s.ItemCondition == "GOOD"),
            Damaged = g.LongCount(s => s.ItemCondition == "DAMAGED"), Awaiting = g.LongCount(s => s.CurrentLocationId == null)
        }).ToListAsync();
        foreach (var row in inventory)
        {
            var warehouse = byWarehouse[row.WarehouseId];
            warehouse.InventoryCount = row.Count;
            warehouse.GoodCount = row.Good;
            warehouse.DamagedCount = row.Damaged;
            warehouse.AwaitingCount = row.Awaiting;
        }

        var locations = await db.StorageLocations.AsNoTracking().Where(l => ids.Contains(l.WarehouseId)).ToListAsync();
        var usage = await stock.Where(s => s.CurrentLocationId != null).GroupBy(s => s.CurrentLocationId!.Value)
            .Select(g => new LocationUsage
            {
                LocationId = g.Key, Count = g.LongCount(),
                Area = g.Sum(s => s.Product.LengthCm * s.Product.WidthCm / 10000m),
                Volume = g.Sum(s => s.Product.LengthCm * s.Product.WidthCm * s.Product.HeightCm / 1000000m),
                Weight = g.Sum(s => s.Product.GrossWeightKg),
                InvalidProfiles = g.LongCount(s => s.Product.LengthCm <= 0 || s.Product.WidthCm <= 0
                    || s.Product.HeightCm <= 0 || s.Product.GrossWeightKg <= 0)
            }).ToDictionaryAsync(s => s.LocationId);
        var locationMap = locations.ToDictionary(l => l.LocationId);
        foreach (var warehouse in model.Warehouses)
        {
            warehouse.ZoneCount = locations.Count(l => l.WarehouseId == warehouse.Id && l.LocationType == "ZONE");
            foreach (var leaf in locations.Where(l => l.WarehouseId == warehouse.Id && l.LocationType is "FLOOR_SLOT" or "TIER"))
            {
                var load = usage.GetValueOrDefault(leaf.LocationId) ?? new LocationUsage();
                if (!IsUsableLeaf(leaf, locationMap, out var inactive))
                {
                    if (!inactive || load.Count > 0) warehouse.InvalidLocationCount++;
                    continue;
                }
                warehouse.LocationCount++;
                if (load.InvalidProfiles > 0) warehouse.InvalidLocationCount++;
                bool floor = leaf.LocationType == "FLOOR_SLOT";
                decimal total = floor ? leaf.LengthCm!.Value * leaf.WidthCm!.Value / 10000m
                    : leaf.LengthCm!.Value * leaf.WidthCm!.Value * leaf.HeightCm!.Value / 1000000m;
                decimal used = floor ? load.Area : load.Volume;
                if (floor) { warehouse.TotalFloorArea += total; warehouse.UsedFloorArea += used; }
                else { warehouse.TotalRackVolume += total; warehouse.UsedRackVolume += used; }
                bool overloaded = used > total || (leaf.MaxWeightKg.HasValue && load.Weight > leaf.MaxWeightKg.Value);
                if (overloaded) warehouse.OverloadedCount++;
                else if (used / total >= .8m || (leaf.MaxWeightKg > 0 && load.Weight / leaf.MaxWeightKg.Value >= .8m))
                    warehouse.NearFullCount++;
            }
        }

        var purchaseProgress = await db.VwPurchaseOrderProgresses.AsNoTracking()
            .Where(p => ids.Contains(p.WarehouseId) && (p.Status == "CONFIRMED" || p.Status == "PARTIALLY_RECEIVED"))
            .GroupBy(p => p.WarehouseId).Select(g => new { Id = g.Key, Quantity = g.Sum(p => p.RemainingQuantity > 0 ? p.RemainingQuantity!.Value : 0) }).ToListAsync();
        foreach (var row in purchaseProgress) byWarehouse[row.Id].IncomingPurchase = row.Quantity;
        var returnProgress = await db.VwReturnProgresses.AsNoTracking()
            .Where(r => ids.Contains(r.WarehouseId) && (r.Status == "OPEN" || r.Status == "PARTIALLY_RECEIVED"))
            .GroupBy(r => r.WarehouseId).Select(g => new { Id = g.Key, Quantity = g.Sum(r => r.RemainingQuantity > 0 ? r.RemainingQuantity!.Value : 0) }).ToListAsync();
        foreach (var row in returnProgress) byWarehouse[row.Id].IncomingReturn = row.Quantity;

        var transfers = await db.TransferOrders.AsNoTracking()
            .Where(o => (ids.Contains(o.FromWarehouseId) || ids.Contains(o.ToWarehouseId))
                && (o.Status == "RELEASED" || o.Status == "IN_PROGRESS"))
            .Select(o => new { Id = o.TransferOrderId, o.Status, o.ExpectedDate,
                HasDocuments = o.TransferDispatches.Any() || o.TransferReceipts.Any() }).ToListAsync();
        var openTransferIds = transfers.Where(t => !TransferPolicy.IsExpired(t.Status, t.ExpectedDate, today, t.HasDocuments))
            .Select(t => t.Id).ToArray();
        var transferProgress = await db.TransferOrderLines.AsNoTracking().Where(l => openTransferIds.Contains(l.TransferOrderId))
            .Select(l => new
            {
                WarehouseId = l.TransferOrder.ToWarehouseId,
                Remaining = (long)l.RequestedQuantity - l.CancelledQuantity
                    - db.TransferReceiptItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.TransferReceipt.Status == "DONE")
                    - db.TransferLossRequestItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.LossRequest.AppliedAt != null)
            }).ToListAsync();
        foreach (var row in transferProgress.Where(t => t.Remaining > 0).GroupBy(t => t.WarehouseId))
            if (byWarehouse.TryGetValue(row.Key, out var warehouse)) warehouse.IncomingTransfer = row.Sum(t => t.Remaining);

        var workload = model.Workload;
        workload.OpenPurchaseOrders = await db.PurchaseOrders.AsNoTracking().CountAsync(p => ids.Contains(p.WarehouseId)
            && (p.Status == "CONFIRMED" || p.Status == "PARTIALLY_RECEIVED"));
        workload.RemainingPurchaseQuantity = model.Warehouses.Sum(w => w.IncomingPurchase);
        workload.OpenSalesOrders = await db.SalesOrders.AsNoTracking().CountAsync(o => ids.Contains(o.WarehouseId)
            && (o.Status == "SUBMITTED" || o.Status == "PARTIALLY_FULFILLED"));
        workload.RemainingSalesQuantity = await db.VwSalesOrderProgresses.AsNoTracking()
            .Where(o => ids.Contains(o.WarehouseId) && (o.Status == "SUBMITTED" || o.Status == "PARTIALLY_FULFILLED"))
            .SumAsync(o => o.RemainingQuantity > 0 ? o.RemainingQuantity!.Value : 0);
        workload.OpenReturns = await db.CustomerReturnOrders.AsNoTracking().CountAsync(o => ids.Contains(o.WarehouseId)
            && (o.Status == "OPEN" || o.Status == "PARTIALLY_RECEIVED"));
        workload.RemainingReturnQuantity = model.Warehouses.Sum(w => w.IncomingReturn);
        workload.OpenTransfers = openTransferIds.Length;
        workload.RemainingTransferQuantity = transferProgress.Sum(t => Math.Max(0, t.Remaining));
        workload.InTransitQuantity = await db.VwTransferInTransits.AsNoTracking().LongCountAsync(t => ids.Contains(t.ToWarehouseId));
        workload.DraftInboundCount = await db.Inbounds.AsNoTracking().CountAsync(i => ids.Contains(i.PurchaseOrder.WarehouseId) && i.Status == "DRAFT")
            + await db.ReturnReceipts.AsNoTracking().CountAsync(r => ids.Contains(r.ReturnOrder.WarehouseId) && r.Status == "DRAFT")
            + await db.TransferReceipts.AsNoTracking().CountAsync(r => ids.Contains(r.TransferOrder.ToWarehouseId) && r.Status == "DRAFT");
        workload.OpenOutboundCount = await db.Outbounds.AsNoTracking().CountAsync(o => ids.Contains(o.SalesOrder.WarehouseId) && OpenOutboundStatuses.Contains(o.Status))
            + await db.TransferDispatches.AsNoTracking().CountAsync(d => ids.Contains(d.TransferOrder.FromWarehouseId) && OpenOutboundStatuses.Contains(d.Status));
        var activeStocktakes = await db.Stocktakes.AsNoTracking().Where(s => ids.Contains(s.WarehouseId) && ActiveStocktakeStatuses.Contains(s.Status))
            .GroupBy(s => s.WarehouseId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync();
        foreach (var row in activeStocktakes) byWarehouse[row.Id].ActiveStocktakes = row.Count;
        workload.ActiveStocktakes = activeStocktakes.Sum(s => s.Count);

        var approvals = db.InventoryAdjustments.AsNoTracking().Where(a => a.Status == "SUBMITTED" && ids.Contains(a.Stocktake.WarehouseId));
        var approvalCounts = await approvals.GroupBy(a => a.Stocktake.WarehouseId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync();
        foreach (var row in approvalCounts) byWarehouse[row.Id].PendingApprovalCount = row.Count;
        workload.PendingApprovalCount = approvalCounts.Sum(a => a.Count);
        var latestApprovals = await approvals.OrderBy(a => a.SubmittedAt ?? a.CreatedAt).ThenBy(a => a.AdjustmentId).Take(5)
            .Select(a => new { a.AdjustmentId, a.AdjustmentCode, a.Stocktake.StocktakeCode,
                Warehouse = a.Stocktake.Warehouse.WarehouseName, a.SubmittedAt,
                Quantity = a.InventoryAdjustmentLineAdjustments.Where(l => l.IsActive && l.QuantityDelta < 0).Sum(l => -(long)l.QuantityDelta) }).ToListAsync();
        model.PendingApprovals = latestApprovals.Select(a => new CeoDashboardApproval(a.AdjustmentId, a.AdjustmentCode,
            a.StocktakeCode, a.Warehouse, a.Quantity, a.SubmittedAt.HasValue ? VietnamTime(a.SubmittedAt.Value) : null)).ToList();

        var movements = db.SerialMovements.AsNoTracking().Where(m => m.OccurredAt >= fromUtc && m.OccurredAt < toUtc
            && ((m.EventType == "PURCHASE_RECEIPT" && m.Inbound != null && m.Inbound.Status == "DONE" && m.ToWarehouseId.HasValue && ids.Contains(m.ToWarehouseId.Value))
                || (m.EventType == "CUSTOMER_RETURN" && m.ReturnReceipt != null && m.ReturnReceipt.Status == "DONE" && m.ToWarehouseId.HasValue && ids.Contains(m.ToWarehouseId.Value))
                || (m.EventType == "TRANSFER_RECEIPT" && m.TransferReceipt != null && m.TransferReceipt.Status == "DONE" && m.ToWarehouseId.HasValue && ids.Contains(m.ToWarehouseId.Value))
                || (m.EventType == "SALES_ISSUE" && m.Outbound != null && m.Outbound.Status == "DONE" && m.FromWarehouseId.HasValue && ids.Contains(m.FromWarehouseId.Value))
                || (m.EventType == "TRANSFER_DISPATCH" && m.Dispatch != null && m.Dispatch.Status == "DONE" && m.FromWarehouseId.HasValue && ids.Contains(m.FromWarehouseId.Value))));
        var days = await movements.GroupBy(m => new { Day = m.OccurredAt.AddHours(7).Date, m.EventType })
            .Select(g => new { g.Key.Day, g.Key.EventType, Count = g.LongCount() }).ToListAsync();
        model.Trend = days.GroupBy(d => d.Day).Select(g => new CeoDashboardDay
        {
            Day = DateOnly.FromDateTime(g.Key), Purchased = g.Where(d => d.EventType == "PURCHASE_RECEIPT").Sum(d => d.Count),
            Sold = g.Where(d => d.EventType == "SALES_ISSUE").Sum(d => d.Count), Returned = g.Where(d => d.EventType == "CUSTOMER_RETURN").Sum(d => d.Count),
            TransferReceived = g.Where(d => d.EventType == "TRANSFER_RECEIPT").Sum(d => d.Count),
            TransferDispatched = g.Where(d => d.EventType == "TRANSFER_DISPATCH").Sum(d => d.Count)
        }).OrderBy(d => d.Day).ToList();
        var recent = await movements.Select(m => new
        {
            m.EventType, m.OccurredAt,
            DocumentId = m.InboundId ?? m.OutboundId ?? m.ReturnReceiptId ?? m.DispatchId ?? m.TransferReceiptId,
            Code = m.EventType == "PURCHASE_RECEIPT" ? m.Inbound!.ReceiptCode
                : m.EventType == "SALES_ISSUE" ? m.Outbound!.IssueCode
                : m.EventType == "CUSTOMER_RETURN" ? m.ReturnReceipt!.ReceiptCode
                : m.EventType == "TRANSFER_DISPATCH" ? m.Dispatch!.DispatchCode : m.TransferReceipt!.ReceiptCode,
            Warehouse = m.EventType == "SALES_ISSUE" || m.EventType == "TRANSFER_DISPATCH" ? m.FromWarehouse!.WarehouseName : m.ToWarehouse!.WarehouseName
        }).GroupBy(m => new { m.EventType, m.DocumentId, m.Code, m.Warehouse })
            .Select(g => new { g.Key.EventType, g.Key.Code, g.Key.Warehouse, Quantity = g.LongCount(), OccurredAt = g.Max(m => m.OccurredAt) })
            .OrderByDescending(m => m.OccurredAt).ThenBy(m => m.Code).Take(8).ToListAsync();
        model.RecentActivity = recent.Select(m => new CeoDashboardActivity(ActivityType(m.EventType), m.Code,
            m.Warehouse, m.Quantity, VietnamTime(m.OccurredAt))).ToList();
        return model;
    }

    private static bool IsUsableLeaf(StorageLocation leaf, IReadOnlyDictionary<int, StorageLocation> locations, out bool inactive)
    {
        inactive = false;
        var chain = new List<StorageLocation>();
        var seen = new HashSet<int>();
        StorageLocation? node = leaf;
        while (node != null)
        {
            if (node.Status != "ACTIVE") { inactive = true; return false; }
            if (!seen.Add(node.LocationId) || chain.Count >= 4 || node.WarehouseId != leaf.WarehouseId) return false;
            chain.Add(node);
            if (node.ParentLocationId is not int parent) break;
            if (!locations.TryGetValue(parent, out node)) return false;
        }
        bool floor = leaf.LocationType == "FLOOR_SLOT";
        if (floor ? chain.Count != 3 || chain[1].LocationType != "ROW" || chain[1].StorageType != "FLOOR"
            : chain.Count != 4 || chain[1].LocationType != "RACK" || chain[2].LocationType != "ROW" || chain[2].StorageType != "RACK") return false;
        var zone = chain[^1];
        string? condition = floor ? leaf.ConditionPolicy : chain[1].ConditionPolicy;
        return zone.LocationType == "ZONE" && zone.ParentLocationId == null && zone.ZoneType is "STANDARD" or "RESERVE"
            && condition is "GOOD" or "DAMAGED" && leaf.CapacityMode == (floor ? "FLOOR_AREA" : "VOLUME")
            && leaf.LengthCm > 0 && leaf.WidthCm > 0 && (floor || leaf.HeightCm > 0)
            && leaf.HeightCm is not <= 0 && leaf.MaxWeightKg is not <= 0;
    }

    private static DateTime VietnamTime(DateTime value) => DateTime.SpecifyKind(value.AddHours(7), DateTimeKind.Unspecified);
    private static string ActivityType(string type) => type switch
    {
        "PURCHASE_RECEIPT" => "Nhập mua", "SALES_ISSUE" => "Xuất bán", "CUSTOMER_RETURN" => "Khách trả",
        "TRANSFER_DISPATCH" => "Gửi chuyển kho", "TRANSFER_RECEIPT" => "Nhận chuyển kho", _ => type
    };
    private sealed class LocationUsage
    {
        public int LocationId { get; set; }
        public long Count { get; set; }
        public decimal Area { get; set; }
        public decimal Volume { get; set; }
        public decimal Weight { get; set; }
        public long InvalidProfiles { get; set; }
    }
}
