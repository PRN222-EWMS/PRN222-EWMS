using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int LockedAccounts { get; set; }
    public int NewAccounts { get; set; }
    public int UnassignedWarehouseStaff { get; set; }
    public List<AuditLog> RecentActivity { get; set; } = [];
}
