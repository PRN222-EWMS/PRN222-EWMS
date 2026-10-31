using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public interface IManagerDashboardService
{
    Task<ManagerDashboardViewModel?> GetDashboardAsync(int? warehouseId);
}