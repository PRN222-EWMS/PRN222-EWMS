using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.WarehouseStaff;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public interface IWarehouseStaffRepository
{
    Task<PRN222_EWMS_Web.Models.User?> GetUserWithWarehouseAsync(int userId);
    Task<PRN222_EWMS_Web.Models.Warehouse?> GetWarehouseByIdAsync(int warehouseId);
    Task<WarehouseDashboardViewModel> GetDashboardMetricsAsync(int warehouseId, int userId);
    Task<WarehousePoListViewModel> GetReceivableOrdersAsync(int warehouseId, string? search, DateOnly? fromDate, DateOnly? toDate, int pageIndex, int pageSize);
    Task<WarehousePoDetailsViewModel?> GetReceivableOrderDetailsAsync(long purchaseOrderId, int warehouseId);
}
