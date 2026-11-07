using PRN222_EWMS_Web.Repositories.Admin;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public class AdminDashboardService(IAdminDashboardRepository repository) : IAdminDashboardService
{
    public async Task<AdminDashboardViewModel> GetDashboardAsync() => new()
    {
        TotalAccounts = await repository.CountAccountsAsync(),
        ActiveAccounts = await repository.CountAccountsByStatusAsync("ACTIVE"),
        LockedAccounts = await repository.CountAccountsByStatusAsync("LOCKED"),
        NewAccounts = await repository.CountAccountsCreatedSinceAsync(DateTime.UtcNow.AddDays(-30)),
        UnassignedWarehouseStaff = await repository.CountUnassignedWarehouseStaffAsync(),
        RecentActivity = await repository.GetRecentActivityAsync(8)
    };
}
