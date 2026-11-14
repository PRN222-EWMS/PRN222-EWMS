using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Warehouse;

public interface IWarehouseRepository
{
    Task<T> InTransactionAsync<T>(int warehouseId, Func<Task<T>> operation);
    Task<bool> HasStockAsync(int warehouseId, IReadOnlyCollection<int>? locationIds);
    Task<List<int?>> GetLockedScopeRootsAsync(int warehouseId);
    Task<List<Category>> GetCategoriesAsync();
    Task<List<Brand>> GetBrandsAsync();
    Task<bool> LocationNameExistsAsync(int warehouseId, int? parentId, string name, int? excludingId);
    Task SaveLocationAsync(StorageLocation location, byte[]? originalRowVersion, IReadOnlyList<ZoneStorageRule> rules,
        IReadOnlyCollection<int> deactivateIds);
    Task<WarehouseEntity?> FindByIdAsync(int id);
    Task<List<StorageLocation>> GetLocationsAsync(int warehouseId);
    Task<List<ZoneStorageRule>> GetZoneRulesAsync(int warehouseId);
    Task<bool> NameExistsAsync(string name, int? excludingId);
    Task AddAsync(WarehouseEntity warehouse);
    Task UpdateAsync(WarehouseEntity warehouse, byte[] originalRowVersion);
    Task<(IReadOnlyList<WarehouseEntity> Items, int TotalCount, int TotalWarehouses, int Page)> GetPageAsync(
        string? search, string? warehouseType, string? status,
        DateOnly? rentalFrom, DateOnly? rentalTo, int page, int pageSize);
}
