using PRN222_EWMS_Web.Repositories.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.WarehouseStaff;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public class WarehouseStaffService(IWarehouseStaffRepository repo) : IWarehouseStaffService
{
    public async Task<WarehouseDashboardViewModel> GetDashboardAsync(int userId)
    {
        var user = await repo.GetUserWithWarehouseAsync(userId);
        if (user == null || !user.WarehouseId.HasValue)
        {
            return new WarehouseDashboardViewModel
            {
                UserFullName = user?.FullName ?? "Nhân viên kho",
                Username = user?.Username ?? "",
                WarehouseName = "Chưa được phân công kho làm việc",
                Address = "Vui lòng liên hệ Quản trị viên (Admin) để được gán kho trước khi tác nghiệp."
            };
        }

        var dashboard = await repo.GetDashboardMetricsAsync(user.WarehouseId.Value, userId);
        dashboard.UserFullName = user.FullName;
        dashboard.Username = user.Username;
        return dashboard;
    }

    public async Task<WarehousePoListViewModel> GetReceivableOrdersAsync(
        int userId,
        string? search,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageIndex,
        int pageSize)
    {
        var user = await repo.GetUserWithWarehouseAsync(userId);
        if (user == null || !user.WarehouseId.HasValue)
        {
            return new WarehousePoListViewModel
            {
                WarehouseName = "Chưa gán kho",
                Items = new()
            };
        }

        return await repo.GetReceivableOrdersAsync(user.WarehouseId.Value, search, fromDate, toDate, pageIndex, pageSize);
    }

    public async Task<WarehousePoDetailsViewModel?> GetReceivableOrderDetailsAsync(long purchaseOrderId, int userId)
    {
        var user = await repo.GetUserWithWarehouseAsync(userId);
        if (user == null || !user.WarehouseId.HasValue)
        {
            return null;
        }

        return await repo.GetReceivableOrderDetailsAsync(purchaseOrderId, user.WarehouseId.Value);
    }
}
