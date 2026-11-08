using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public interface IManagerStocktakeService
{
    Task<ManagerStocktakeListViewModel> GetListAsync(ManagerStocktakeFilter filter, CancellationToken ct);
    Task<ManagerStocktakeDetailViewModel?> GetDetailsAsync(long id, ManagerStocktakeDetailFilter filter, CancellationToken ct);
    Task<ManagerStocktakeFormViewModel> CreateFormAsync(int? warehouseId, CancellationToken ct);
    Task<ManagerStocktakeFormViewModel?> EditFormAsync(long id, CancellationToken ct);
    Task PopulateFormAsync(ManagerStocktakeFormViewModel form, CancellationToken ct);
    Task<long> SaveAsync(ManagerStocktakeFormViewModel form, int actorId, CancellationToken ct);
    Task DeleteAsync(ManagerStocktakeDeleteInput input, int actorId, CancellationToken ct);
}

public class ManagerStocktakeException(string message, string? field = null, int statusCode = 400) : Exception(message)
{
    public string? Field { get; } = field;
    public int StatusCode { get; } = statusCode;
}
