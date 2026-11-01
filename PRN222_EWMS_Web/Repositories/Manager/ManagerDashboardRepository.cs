using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public class ManagerDashboardRepository(EwmsDbContext db)
    : IManagerDashboardRepository
{
    public async Task<ManagerDashboardViewModel?> GetDashboardAsync(
        int? warehouseId)
    {
        var warehouses = await db.Warehouses
            .AsNoTracking()
            .OrderBy(w => w.WarehouseName)
            .Select(w => new
            {
                w.WarehouseId,
                w.WarehouseName
            })
            .ToListAsync();

        if (warehouseId.HasValue &&
            !warehouses.Any(w => w.WarehouseId == warehouseId.Value))
        {
            return null;
        }

        var inbounds = db.Inbounds.AsNoTracking();
        var outbounds = db.Outbounds.AsNoTracking();
        var purchases = db.PurchaseOrders.AsNoTracking();
        var sales = db.SalesOrders.AsNoTracking();
        var stocktakes = db.Stocktakes.AsNoTracking();
        var capacities = db.VwLocationCapacities.AsNoTracking();

        if (warehouseId.HasValue)
        {
            int selectedId = warehouseId.Value;

            inbounds = inbounds.Where(i =>
                i.PurchaseOrder.WarehouseId == selectedId);

            outbounds = outbounds.Where(o =>
                o.SalesOrder.WarehouseId == selectedId);

            purchases = purchases.Where(p =>
                p.WarehouseId == selectedId);

            sales = sales.Where(s =>
                s.WarehouseId == selectedId);

            stocktakes = stocktakes.Where(s =>
                s.WarehouseId == selectedId);

            capacities = capacities.Where(c =>
                c.WarehouseId == selectedId);
        }

        var model = new ManagerDashboardViewModel
        {
            WarehouseId = warehouseId,

            Warehouses = warehouses.Select(w => new SelectListItem
            {
                Value = w.WarehouseId.ToString(),
                Text = w.WarehouseName
            }).ToList()
        };

        // Inbound trong DB có DRAFT, DONE, CANCELLED.
        model.PendingInboundCount = await inbounds
            .CountAsync(i => i.Status == "DRAFT");

        model.PendingOutboundCount = await outbounds.CountAsync(o =>
            o.Status == "DRAFT" ||
            o.Status == "READY" ||
            o.Status == "PICKING");

        // Đơn mua đã xác nhận nhưng chưa nhận đủ.
        model.OpenPurchaseOrderCount = await purchases.CountAsync(p =>
            p.Status == "CONFIRMED" ||
            p.Status == "PARTIALLY_RECEIVED");

        // Đang kiểm đếm hoặc xử lý kết quả kiểm kê.
        model.ActiveStocktakeCount = await stocktakes.CountAsync(s =>
            s.Status == "COUNTING" ||
            s.Status == "RECOUNTING" ||
            s.Status == "REVIEWING" ||
            s.Status == "PENDING_APPROVAL");

        model.PurchaseStatuses = await purchases
            .GroupBy(p => p.Status)
            .Select(g => new DashboardStatusItem
            {
                Status = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Status)
            .ToListAsync();

        model.SalesStatuses = await sales
            .GroupBy(s => s.Status)
            .Select(g => new DashboardStatusItem
            {
                Status = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Status)
            .ToListAsync();

        var recentInbounds = await inbounds
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.InboundId)
            .Take(10)
            .Select(i => new DashboardReceiptItem
            {
                Type = "Nhập kho",
                Code = i.ReceiptCode,
                WarehouseName = i.PurchaseOrder.Warehouse.WarehouseName,
                Status = i.Status,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        var recentOutbounds = await outbounds
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OutboundId)
            .Take(10)
            .Select(o => new DashboardReceiptItem
            {
                Type = "Xuất kho",
                Code = o.IssueCode,
                WarehouseName = o.SalesOrder.Warehouse.WarehouseName,
                Status = o.Status,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        model.RecentReceipts = recentInbounds
            .Concat(recentOutbounds)
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Type)
            .ThenBy(r => r.Code)
            .Take(10)
            .ToList();

        model.Capacities = await capacities
            .OrderBy(c => c.WarehouseId)
            .ThenBy(c => c.LocationName)
            .ToListAsync();

        // 80% is the current near-full threshold; overloaded locations are counted separately.
        bool IsOverloaded(VwLocationCapacity location) =>
            (location.CapacityAreaM2.HasValue && location.UsedAreaM2.HasValue &&
             location.UsedAreaM2.Value > location.CapacityAreaM2.Value) ||
            (location.CapacityVolumeM3.HasValue && location.UsedVolumeM3.HasValue &&
             location.UsedVolumeM3.Value > location.CapacityVolumeM3.Value) ||
            (location.MaxWeightKg.HasValue &&
             location.UsedWeightKg > location.MaxWeightKg.Value);

        bool IsNearFull(VwLocationCapacity location) => !IsOverloaded(location) &&
            ((location.CapacityAreaM2 > 0 && location.UsedAreaM2.HasValue &&
              location.UsedAreaM2.Value / location.CapacityAreaM2.Value >= 0.8m) ||
             (location.CapacityVolumeM3 > 0 && location.UsedVolumeM3.HasValue &&
              location.UsedVolumeM3.Value / location.CapacityVolumeM3.Value >= 0.8m) ||
             (location.MaxWeightKg > 0 &&
              location.UsedWeightKg / location.MaxWeightKg.Value >= 0.8m));

        bool IsMissingCapacity(VwLocationCapacity location) => location.CapacityMode switch
        {
            "FLOOR_AREA" => !location.CapacityAreaM2.HasValue ||
                            location.CapacityAreaM2 <= 0 || !location.UsedAreaM2.HasValue,
            "VOLUME" => !location.CapacityVolumeM3.HasValue ||
                        location.CapacityVolumeM3 <= 0 || !location.UsedVolumeM3.HasValue,
            _ => true
        };

        var locationsByWarehouse = model.Capacities.ToLookup(c => c.WarehouseId);
        model.WarehouseCapacities = warehouses
            .Where(w => !warehouseId.HasValue || w.WarehouseId == warehouseId.Value)
            .Select(w =>
            {
                var locations = locationsByWarehouse[w.WarehouseId].ToList();
                return new WarehouseCapacitySummary
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseName = w.WarehouseName,
                    TotalAreaM2 = locations.Where(c => c.CapacityMode == "FLOOR_AREA")
                        .Sum(c => c.CapacityAreaM2 ?? 0),
                    UsedAreaM2 = locations.Where(c => c.CapacityMode == "FLOOR_AREA")
                        .Sum(c => c.UsedAreaM2 ?? 0),
                    TotalVolumeM3 = locations.Where(c => c.CapacityMode == "VOLUME")
                        .Sum(c => c.CapacityVolumeM3 ?? 0),
                    UsedVolumeM3 = locations.Where(c => c.CapacityMode == "VOLUME")
                        .Sum(c => c.UsedVolumeM3 ?? 0),
                    LocationCount = locations.Count,
                    OverloadedLocationCount = locations.Count(IsOverloaded),
                    NearFullLocationCount = locations.Count(IsNearFull),
                    MissingCapacityLocationCount = locations.Count(IsMissingCapacity)
                };
            }).ToList();

        return model;
    }
}
