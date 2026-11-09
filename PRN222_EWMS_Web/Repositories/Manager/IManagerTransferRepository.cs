using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public interface IManagerTransferRepository
{
    Task<int> CountAsync(string? search, string? status);
    Task<List<ManagerTransferSummary>> GetListAsync(string? search, string? status, int skip, int take);
    Task<List<string>> GetStatusOptionsAsync();
    Task<ManagerTransferDetail?> GetDetailAsync(long id);
}
