using PRN222_EWMS_Web.Repositories.Manager;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public class ManagerDashboardService(
    IManagerDashboardRepository repository)
    : IManagerDashboardService
{
    public Task<ManagerDashboardViewModel?> GetDashboardAsync(
        int? warehouseId)
    {
        return repository.GetDashboardAsync(warehouseId);
    }
}