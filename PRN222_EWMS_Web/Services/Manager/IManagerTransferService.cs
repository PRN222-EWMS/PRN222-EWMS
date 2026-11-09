using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public interface IManagerTransferService
{
    Task<ManagerTransferListViewModel> GetListAsync(string? search, string? status);
    Task<ManagerTransferDetail?> GetDetailAsync(long id);
}
