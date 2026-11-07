using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public interface IManagerInboundService
{
    Task<ManagerInboundListViewModel> GetListAsync(ManagerInboundFilter filter);
    Task<ManagerInboundDetailViewModel?> GetDetailAsync(long id);
}
