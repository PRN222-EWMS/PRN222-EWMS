using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Repositories.Ceo;

public interface ICeoDashboardRepository
{
    Task<CeoDashboardViewModel> GetDashboardAsync(CeoDashboardFilter filter,
        DateTime fromUtc, DateTime toUtc, DateOnly today);
}
