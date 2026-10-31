using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public interface IManagerDashboardRepository
{
    Task<ManagerDashboardViewModel?> GetDashboardAsync(int? warehouseId);
}