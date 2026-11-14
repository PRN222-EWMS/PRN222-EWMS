using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public interface IManagerStocktakeRepository
{
    Task<bool> IsActiveManagerAsync(int userId);
    Task<List<ManagerStocktakeWarehouseOption>> GetWarehousesAsync();
    Task<ManagerStocktakePlanningData?> GetPlanningDataAsync(int warehouseId);
    Task<ManagerStocktakeListViewModel> GetListAsync(ManagerStocktakeFilter filter);
    Task<ManagerStocktakeDetailViewModel?> GetDetailsAsync(long id, ManagerStocktakeDetailFilter filter);
    Task<Stocktake?> FindAsync(long id, bool forUpdate);
    Task<List<StocktakeScope>> GetScopesAsync(long id);
    Task<List<StocktakeAssignment>> GetAssignmentsAsync(long id);
    Task<bool> HasExecutionDataAsync(long id);
    Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> operation);
    Task InsertHeaderAsync(Stocktake plan);
    Task SaveHeaderAsync(Stocktake plan);
    Task ClearPlanChildrenAsync(long id);
    Task InsertPlanChildrenAsync(Stocktake plan, IReadOnlyCollection<int> scopeIds, IReadOnlyCollection<ManagerStocktakeAssignmentInput> assignments);
    Task DeleteHeaderAsync(Stocktake plan);
}
