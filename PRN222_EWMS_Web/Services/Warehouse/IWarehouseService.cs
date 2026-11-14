using PRN222_EWMS_Web.ViewModels.Warehouse;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface IWarehouseService
{
    Task<WarehouseListViewModel> GetListAsync(WarehouseListFilter filter);
    Task<WarehouseDetailsViewModel?> GetDetailsAsync(int id);
    Task<WarehouseFormViewModel?> GetEditAsync(int id);
    Task<WarehouseSaveResult> CreateAsync(WarehouseFormViewModel model);
    Task<WarehouseSaveResult> UpdateAsync(int id, WarehouseFormViewModel model);
    Task<StorageLocationFormViewModel?> GetLocationFormAsync(int warehouseId, int? locationId, int? parentId);
    Task PopulateLocationOptionsAsync(StorageLocationFormViewModel model);
    Task<WarehouseSaveResult> SaveLocationAsync(int warehouseId, int? locationId, StorageLocationFormViewModel model);
}
