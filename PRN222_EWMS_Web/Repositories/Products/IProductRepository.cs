using PRN222_EWMS_Web.Models;

using PRN222_EWMS_Web.ViewModels.Products;

namespace PRN222_EWMS_Web.Repositories.Products;

public interface IProductRepository
{
    Task<List<PRN222_EWMS_Web.Models.Warehouse>> GetWarehousesAsync();
    Task<List<ProductStockCount>> GetStockCountsAsync(int? warehouseId);
    Task<int?> GetWarehouseIdAsync(int userId);

    Task<List<Product>> GetListAsync(
        string? keyword, int? warehouseId);

    Task<Product?> GetDetailAsync(
        int id, int? warehouseId);
    Task<List<Category>> GetCategoriesAsync();
    Task<List<Brand>> GetBrandsAsync();
    Task<bool> ProductCodeExistsAsync(string code);
    Task<bool> CategoryExistsAsync(int id);
    Task<bool> BrandExistsAsync(int id);
    Task<int> CreateAsync(Product product);
    Task<Product?> GetForEditAsync(int id);
    Task<bool> HasItemsInWarehouseAsync(int productId);
    Task SaveChangesAsync();
    Task<string?> GetZoneRuleErrorAsync(
    int productId, int categoryId, int brandId);
}
