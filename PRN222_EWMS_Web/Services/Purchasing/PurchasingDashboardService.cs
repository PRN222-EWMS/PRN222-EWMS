using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Repositories.Purchasing;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Services.Purchasing;

public class PurchasingDashboardService(
    IPurchaseOrderRepository poRepo,
    IUserRepository userRepo) : IPurchasingDashboardService
{
    public async Task<PurchasingDashboardViewModel> GetDashboardAsync(int currentUserId)
    {
        var user = await userRepo.FindByIdAsync(currentUserId);
        var fullName = user?.FullName ?? "Nhân viên Mua hàng";

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Scope: Toàn bộ bộ phận mua hàng (tất cả các đơn mua hàng trong hệ thống)
        var allOrdersQuery = poRepo.Orders.AsNoTracking();

        // 1. KPI: Đơn nháp cần xác nhận
        var draftCount = await allOrdersQuery.CountAsync(o => o.Status == "DRAFT");

        // 2. KPI: Đơn đang chờ kho tiếp nhận (CONFIRMED hoặc PARTIALLY_RECEIVED)
        var waitingInboundCount = await allOrdersQuery.CountAsync(o => o.Status == "CONFIRMED" || o.Status == "PARTIALLY_RECEIVED");

        // 3. KPI: Đơn quá hạn giao hàng
        var overdueCount = await allOrdersQuery.CountAsync(o => 
            (o.Status == "CONFIRMED" || o.Status == "PARTIALLY_RECEIVED") && o.ExpectedArrivalDate < today);

        // 4. KPI: Số nhà cung cấp đang hoạt động trong toàn hệ thống
        var activeSupplierCount = await poRepo.Suppliers.AsNoTracking().CountAsync(s => s.Status == "ACTIVE");

        // Lấy danh sách 5 đơn mua hàng gần nhất của toàn bộ phận
        var recentOrdersRaw = await allOrdersQuery
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new
            {
                o.PurchaseOrderId,
                o.Pocode,
                SupplierName = o.Supplier.SupplierName,
                WarehouseName = o.Warehouse.WarehouseName,
                CreatedByName = o.CreatedByUser.FullName,
                o.CreatedByUserId,
                o.OrderDate,
                o.ExpectedArrivalDate,
                o.Status,
                o.ConfirmedAt
            })
            .ToListAsync();

        var poIds = recentOrdersRaw.Select(r => r.PurchaseOrderId).ToList();

        // Lấy tiến độ tương ứng từ VwPurchaseOrderProgress
        var progressList = await poRepo.Progress.AsNoTracking()
            .Where(p => poIds.Contains(p.PurchaseOrderId))
            .GroupBy(p => p.PurchaseOrderId)
            .Select(g => new
            {
                PurchaseOrderId = g.Key,
                TotalOrdered = g.Sum(x => x.OrderedQuantity),
                TotalClosed = g.Sum(x => x.ClosedQuantity),
                TotalReceived = g.Sum(x => x.ReceivedQuantity),
                TotalRemaining = g.Sum(x => x.RemainingQuantity ?? 0)
            })
            .ToListAsync();

        var progressDict = progressList.ToDictionary(p => p.PurchaseOrderId);

        var recentOrderDtos = recentOrdersRaw.Select(r =>
        {
            progressDict.TryGetValue(r.PurchaseOrderId, out var prog);
            return new PoListItemDto
            {
                PurchaseOrderId = r.PurchaseOrderId,
                Pocode = r.Pocode,
                SupplierName = r.SupplierName,
                WarehouseName = r.WarehouseName,
                CreatedByName = r.CreatedByName,
                CreatedByUserId = r.CreatedByUserId,
                OrderDate = r.OrderDate,
                ExpectedArrivalDate = r.ExpectedArrivalDate,
                Status = r.Status,
                ConfirmedAt = r.ConfirmedAt,
                TotalOrderedQuantity = prog?.TotalOrdered ?? 0,
                TotalClosedQuantity = prog?.TotalClosed ?? 0,
                TotalReceivedQuantity = prog?.TotalReceived ?? 0,
                TotalRemainingQuantity = prog?.TotalRemaining ?? 0,
                CanEdit = (r.CreatedByUserId == currentUserId) && (r.Status == "DRAFT" || r.Status == "CONFIRMED")
            };
        }).ToList();

        return new PurchasingDashboardViewModel
        {
            DraftCount = draftCount,
            WaitingInboundCount = waitingInboundCount,
            OverdueCount = overdueCount,
            ActiveSupplierCount = activeSupplierCount,
            UserFullName = fullName,
            CurrentUserId = currentUserId,
            RecentOrders = recentOrderDtos
        };
    }
}
