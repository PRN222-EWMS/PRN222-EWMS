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

        return model;
    }
}