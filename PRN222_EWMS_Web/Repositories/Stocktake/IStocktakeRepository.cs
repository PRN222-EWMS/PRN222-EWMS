namespace PRN222_EWMS_Web.Repositories.Stocktaking;

public interface IStocktakeRepository
{
    IQueryable<T> Query<T>() where T : class;
    void Add<T>(T entity) where T : class;
    void TouchPlan(Models.Stocktake plan);
    Task<List<Models.AuditLog>> ReadStocktakeHistoryAsync(string tableName, long? planId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<T> TransactionAsync<T>(int actorId, int warehouseId, Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
