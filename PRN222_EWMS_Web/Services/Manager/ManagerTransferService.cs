using PRN222_EWMS_Web.Repositories.Manager;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public class ManagerTransferService(IManagerTransferRepository repository)
    : IManagerTransferService
{
    public async Task<ManagerTransferListViewModel> GetListAsync(string? search, string? status)
    {
        search = search?.Trim();
        status = status?.Trim();

        return new ManagerTransferListViewModel
        {
            Search = search,
            Status = status,
            StatusOptions = await repository.GetStatusOptionsAsync(),
            Orders = await repository.GetListAsync(search, status)
        };
    }

    public Task<ManagerTransferDetail?> GetDetailAsync(long id)
    {
        return repository.GetDetailAsync(id);
    }
}
