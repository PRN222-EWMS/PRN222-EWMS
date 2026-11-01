using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Products;

namespace PRN222_EWMS_Web.Services.Products;

public interface IProductService
{
    Task<ProductListViewModel?> GetProductListPageAsync(
        string? keyword, int? warehouseId, int userId,
        bool isManager, bool isWarehouseStaff, bool isPurchasingStaff = false);
    Task<List<Product>> GetListAsync(
        string? keyword, int userId, bool isWarehouseStaff);

    Task<Product?> GetDetailAsync(
        int id, int userId, bool isWarehouseStaff);

    Task LoadCreateOptionsAsync(ProductCreateViewModel model);

    Task<(int? Id, string? Field, string? Error)> CreateAsync(
        ProductCreateViewModel model, int userId);

    Task<ProductEditViewModel?> GetEditAsync(int id);

    Task<(bool Success, string? Field, string? Error)> UpdateAsync(
        int id, ProductEditViewModel model);
}
