using PRN222_EWMS_Web.ViewModels.WarehouseStaff;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public interface IWarehouseStaffService
{
    Task<WarehouseDashboardViewModel> GetDashboardAsync(int userId);
    Task<WarehousePoListViewModel> GetReceivableOrdersAsync(int userId, string? search, DateOnly? fromDate, DateOnly? toDate, int pageIndex, int pageSize);
    Task<WarehousePoDetailsViewModel?> GetReceivableOrderDetailsAsync(long purchaseOrderId, int userId);
}
