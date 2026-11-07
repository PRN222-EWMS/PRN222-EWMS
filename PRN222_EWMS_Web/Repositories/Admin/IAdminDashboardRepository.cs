using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Admin;

public interface IAdminDashboardRepository
{
    Task<int> CountAccountsAsync();
    Task<int> CountAccountsByStatusAsync(string status);
    Task<int> CountAccountsCreatedSinceAsync(DateTime since);
    Task<int> CountUnassignedWarehouseStaffAsync();
    Task<List<AuditLog>> GetRecentActivityAsync(int count);
}
