using PRN222_EWMS_Web.Repositories.Manager;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public class ManagerTransferService(IManagerTransferRepository repository)
    : IManagerTransferService
{
    public async Task<ManagerTransferListViewModel> GetListAsync(string? search, string? status, int page = 1)
    {
        search = search?.Trim();
        status = status?.Trim();

        const int pageSize = 10;
        var count = await repository.CountAsync(search, status);
        var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        return new ManagerTransferListViewModel
        {
            Search = search,
            Status = status,
            StatusOptions = await repository.GetStatusOptionsAsync(),
            Page = page,
            TotalPages = totalPages,
            Orders = await repository.GetListAsync(search, status, (page - 1) * pageSize, pageSize)
        };
    }

    public Task<ManagerTransferDetail?> GetDetailAsync(long id)
    {
        return repository.GetDetailAsync(id);
    }
}
