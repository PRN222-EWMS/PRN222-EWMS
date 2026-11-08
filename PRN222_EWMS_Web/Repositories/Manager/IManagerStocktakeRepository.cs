using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public interface IManagerStocktakeRepository
{
    Task<bool> IsActiveManagerAsync(int userId, CancellationToken ct);
    Task<List<ManagerStocktakeWarehouseOption>> GetWarehousesAsync(CancellationToken ct);
    Task<ManagerStocktakePlanningData?> GetPlanningDataAsync(int warehouseId, CancellationToken ct);
    Task<ManagerStocktakeListViewModel> GetListAsync(ManagerStocktakeFilter filter, CancellationToken ct);
    Task<ManagerStocktakeDetailViewModel?> GetDetailsAsync(long id, ManagerStocktakeDetailFilter filter, CancellationToken ct);
    Task<Stocktake?> FindAsync(long id, bool forUpdate, CancellationToken ct);
    Task<List<StocktakeScope>> GetScopesAsync(long id, CancellationToken ct);
    Task<List<StocktakeAssignment>> GetAssignmentsAsync(long id, CancellationToken ct);
    Task<bool> HasExecutionDataAsync(long id, CancellationToken ct);
    Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> operation, CancellationToken ct);
    Task InsertHeaderAsync(Stocktake plan, CancellationToken ct);
    Task SaveHeaderAsync(Stocktake plan, CancellationToken ct);
    Task ClearPlanChildrenAsync(long id, CancellationToken ct);
    Task InsertPlanChildrenAsync(Stocktake plan, IReadOnlyCollection<int> scopeIds, IReadOnlyCollection<ManagerStocktakeAssignmentInput> assignments, CancellationToken ct);
    Task DeleteHeaderAsync(Stocktake plan, CancellationToken ct);
}
