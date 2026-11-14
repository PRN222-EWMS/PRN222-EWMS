using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Services.Ceo;

public interface ICeoDashboardService
{
    Task<CeoDashboardViewModel> GetDashboardAsync(CeoDashboardFilter filter);
}
