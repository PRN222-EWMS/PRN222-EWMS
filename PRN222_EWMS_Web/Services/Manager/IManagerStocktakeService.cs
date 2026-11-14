using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public interface IManagerStocktakeService
{
    Task<ManagerStocktakeListViewModel> GetListAsync(ManagerStocktakeFilter filter);
    Task<ManagerStocktakeDetailViewModel?> GetDetailsAsync(long id, ManagerStocktakeDetailFilter filter);
    Task<ManagerStocktakeFormViewModel> CreateFormAsync(int? warehouseId);
    Task<ManagerStocktakeFormViewModel?> EditFormAsync(long id);
    Task PopulateFormAsync(ManagerStocktakeFormViewModel form);
    Task<long> SaveAsync(ManagerStocktakeFormViewModel form, int actorId);
    Task DeleteAsync(ManagerStocktakeDeleteInput input, int actorId);
}

public class ManagerStocktakeException(string message, string? field = null, int statusCode = 400) : Exception(message)
{
    public string? Field { get; } = field;
    public int StatusCode { get; } = statusCode;
}
