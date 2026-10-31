using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Warehouse;

public interface IWarehouseRepository
{
    Task<T> InTransactionAsync<T>(int warehouseId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task<bool> HasStockAsync(int warehouseId, IReadOnlyCollection<int>? locationIds, CancellationToken cancellationToken = default);
    Task<List<int?>> GetLockedScopeRootsAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<List<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<bool> LocationNameExistsAsync(int warehouseId, int? parentId, string name, int? excludingId, CancellationToken cancellationToken = default);
    Task SaveLocationAsync(StorageLocation location, byte[]? originalRowVersion, IReadOnlyList<ZoneStorageRule> rules,
        IReadOnlyCollection<int> deactivateIds, CancellationToken cancellationToken = default);
    Task<WarehouseEntity?> FindByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<StorageLocation>> GetLocationsAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<List<ZoneStorageRule>> GetZoneRulesAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken = default);
    Task AddAsync(WarehouseEntity warehouse, CancellationToken cancellationToken = default);
    Task UpdateAsync(WarehouseEntity warehouse, byte[] originalRowVersion, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WarehouseEntity> Items, int TotalCount, int TotalWarehouses, int Page)> GetPageAsync(
        string? search, string? warehouseType, string? status,
        DateOnly? rentalFrom, DateOnly? rentalTo, int page, int pageSize,
        CancellationToken cancellationToken = default);
}
