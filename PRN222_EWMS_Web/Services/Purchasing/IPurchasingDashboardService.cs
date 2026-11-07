using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Services.Purchasing;

public interface IPurchasingDashboardService
{
    Task<PurchasingDashboardViewModel> GetDashboardAsync(int currentUserId);
}

