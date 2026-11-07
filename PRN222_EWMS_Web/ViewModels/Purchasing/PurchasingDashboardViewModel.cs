using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.ViewModels.Purchasing;

public class PurchasingDashboardViewModel
{
    // 4 KPI Cards
    public int DraftCount { get; set; }
    public int WaitingInboundCount { get; set; }
    public int OverdueCount { get; set; }
    public int ActiveSupplierCount { get; set; }

    // User scope information
    public string UserFullName { get; set; } = "";
    public int CurrentUserId { get; set; }

    // Recent & Urgent POs
    public List<PoListItemDto> RecentOrders { get; set; } = [];
}

