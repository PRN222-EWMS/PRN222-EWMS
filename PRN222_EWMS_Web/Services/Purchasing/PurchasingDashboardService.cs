using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Repositories.Purchasing;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Services.Purchasing;

public class PurchasingDashboardService(
    IPurchaseOrderRepository poRepo,
    IUserRepository userRepo,
    IPurchaseOrderService poService) : IPurchasingDashboardService
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

        var recentOrders = await poService.GetOrdersAsync(
            new PoListViewModel { PageSize = 5 }, currentUserId, "PURCHASING_STAFF");

        return new PurchasingDashboardViewModel
        {
            DraftCount = draftCount,
            WaitingInboundCount = waitingInboundCount,
            OverdueCount = overdueCount,
            ActiveSupplierCount = activeSupplierCount,
            UserFullName = fullName,
            CurrentUserId = currentUserId,
            RecentOrders = recentOrders.Items
        };
    }
}
