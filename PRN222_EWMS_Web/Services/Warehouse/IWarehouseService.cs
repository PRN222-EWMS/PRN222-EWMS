using PRN222_EWMS_Web.ViewModels.Warehouse;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface IWarehouseService
{
    Task<WarehouseListViewModel> GetListAsync(WarehouseListFilter filter, CancellationToken cancellationToken = default);
    Task<WarehouseDetailsViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<WarehouseFormViewModel?> GetEditAsync(int id, CancellationToken cancellationToken = default);
    Task<WarehouseSaveResult> CreateAsync(WarehouseFormViewModel model, CancellationToken cancellationToken = default);
    Task<WarehouseSaveResult> UpdateAsync(int id, WarehouseFormViewModel model, CancellationToken cancellationToken = default);
    Task<StorageLocationFormViewModel?> GetLocationFormAsync(int warehouseId, int? locationId, int? parentId, CancellationToken cancellationToken = default);
    Task PopulateLocationOptionsAsync(StorageLocationFormViewModel model, CancellationToken cancellationToken = default);
    Task<WarehouseSaveResult> SaveLocationAsync(int warehouseId, int? locationId, StorageLocationFormViewModel model, CancellationToken cancellationToken = default);
}
