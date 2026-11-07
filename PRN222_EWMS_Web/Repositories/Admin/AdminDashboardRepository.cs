using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Admin;

public class AdminDashboardRepository(EwmsDbContext db) : IAdminDashboardRepository
{
    public Task<int> CountAccountsAsync() => db.Users.CountAsync();
    public Task<int> CountAccountsByStatusAsync(string status) => db.Users.CountAsync(user => user.Status == status);
    public Task<int> CountAccountsCreatedSinceAsync(DateTime since) => db.Users.CountAsync(user => user.CreatedAt >= since);
    public Task<int> CountUnassignedWarehouseStaffAsync() => db.Users.CountAsync(user => user.Role.RoleName == "WAREHOUSE_STAFF" && user.WarehouseId == null);
    public Task<List<AuditLog>> GetRecentActivityAsync(int count) => db.AuditLogs.AsNoTracking().Include(log => log.ActorUser)
        .OrderByDescending(log => log.OccurredAt).ThenByDescending(log => log.AuditId).Take(count).ToListAsync();
}
