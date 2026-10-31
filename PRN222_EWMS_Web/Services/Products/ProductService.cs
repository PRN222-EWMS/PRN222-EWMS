using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Products;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.ViewModels.Products;

namespace PRN222_EWMS_Web.Services.Products;

public class ProductService(IProductRepository repository)
    : IProductService
{
    public async Task<List<Product>> GetListAsync(
        string? keyword, int userId, bool isWarehouseStaff)
    {
        int? warehouseId = null;

        if (isWarehouseStaff)
        {
            warehouseId = await repository.GetWarehouseIdAsync(userId);

            // Staff chưa được gán kho thì không được xem toàn bộ.
            if (warehouseId is null)
                return new List<Product>();
        }

        return await repository.GetListAsync(keyword, warehouseId);
    }

    public async Task<Product?> GetDetailAsync(
        int id, int userId, bool isWarehouseStaff)
    {
        int? warehouseId = null;

        if (isWarehouseStaff)
        {
            warehouseId = await repository.GetWarehouseIdAsync(userId);

            if (warehouseId is null)
                return null;
        }

        return await repository.GetDetailAsync(id, warehouseId);
    }

    public async Task LoadCreateOptionsAsync(
    ProductCreateViewModel model)
    {
        var categories = await repository.GetCategoriesAsync();
        var brands = await repository.GetBrandsAsync();

        model.Categories = categories.Select(c => new SelectListItem
        {
            Value = c.CategoryId.ToString(),
            Text = c.CategoryName
        }).ToList();

        model.Brands = brands.Select(b => new SelectListItem
        {
            Value = b.BrandId.ToString(),
            Text = b.BrandName
        }).ToList();
    }

    public async Task<(int? Id, string? Field, string? Error)> CreateAsync(
        ProductCreateViewModel model, int userId)
    {
        var code = model.ProductCode.Trim();

        if (await repository.ProductCodeExistsAsync(code))
        {
            return (null, nameof(model.ProductCode),
                "Mã sản phẩm đã tồn tại.");
        }

        if (!await repository.CategoryExistsAsync(model.CategoryId))
        {
            return (null, nameof(model.CategoryId),
                "Danh mục không tồn tại.");
        }

        if (!await repository.BrandExistsAsync(model.BrandId))
        {
            return (null, nameof(model.BrandId),
                "Thương hiệu không tồn tại.");
        }

        var product = new Product
        {
            ProductCode = code,
            ProductName = model.ProductName.Trim(),
            Model = model.Model.Trim(),
            Unit = model.Unit.Trim(),
            CategoryId = model.CategoryId,
            BrandId = model.BrandId,
            Specifications = string.IsNullOrWhiteSpace(model.Specifications)
                ? null
                : model.Specifications.Trim(),
            LengthCm = model.LengthCm,
            WidthCm = model.WidthCm,
            HeightCm = model.HeightCm,
            GrossWeightKg = model.GrossWeightKg,
            Status = "ACTIVE",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var id = await repository.CreateAsync(product);
            return (id, null, null);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql
                  && (sql.Number == 2601 || sql.Number == 2627))
        {
            // Kiểm tra trước không ngăn được hai yêu cầu tạo cùng lúc.
            return (null, "",
                "Dữ liệu bị trùng. Vui lòng kiểm tra mã sản phẩm.");
        }
    }

    public async Task<ProductEditViewModel?> GetEditAsync(int id)
    {
        var product = await repository.GetForEditAsync(id);

        if (product is null)
            return null;

        var model = new ProductEditViewModel
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Model = product.Model,
            Unit = product.Unit,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            Specifications = product.Specifications,
            LengthCm = product.LengthCm,
            WidthCm = product.WidthCm,
            HeightCm = product.HeightCm,
            GrossWeightKg = product.GrossWeightKg,
            RowVersion = Convert.ToBase64String(product.RowVersion)
        };

        await LoadCreateOptionsAsync(model);

        return model;
    }

    public async Task<(bool Success, string? Field, string? Error)> UpdateAsync(
        int id, ProductEditViewModel model)
    {
        var product = await repository.GetForEditAsync(id);

        if (product is null)
            return (false, "", "Sản phẩm không còn tồn tại.");

        if (Convert.ToBase64String(product.RowVersion) != model.RowVersion)
        {
            return (false, "",
                "Sản phẩm đã được người khác cập nhật. Hãy tải lại trang.");
        }

        // Mã sản phẩm không được thay đổi trong chức năng này.
        if (product.ProductCode != model.ProductCode)
        {
            return (false, nameof(model.ProductCode),
                "Không được thay đổi mã sản phẩm.");
        }

        if (!await repository.CategoryExistsAsync(model.CategoryId))
        {
            return (false, nameof(model.CategoryId),
                "Danh mục không tồn tại.");
        }

        if (!await repository.BrandExistsAsync(model.BrandId))
        {
            return (false, nameof(model.BrandId),
                "Thương hiệu không tồn tại.");
        }

        bool dimensionsOrWeightChanged =
    product.LengthCm != model.LengthCm ||
    product.WidthCm != model.WidthCm ||
    product.HeightCm != model.HeightCm ||
    product.GrossWeightKg != model.GrossWeightKg;

        if (dimensionsOrWeightChanged &&
            await repository.HasItemsInWarehouseAsync(id))
        {
            return (false, "",
                "Sản phẩm đang có hàng trong kho. " +
                "Không được thay đổi kích thước hoặc trọng lượng.");
        }

        bool categoryOrBrandChanged =
            product.CategoryId != model.CategoryId ||
            product.BrandId != model.BrandId;

        if (categoryOrBrandChanged)
        {
            var error = await repository.GetZoneRuleErrorAsync(
                id,
                model.CategoryId,
                model.BrandId);

            if (error is not null)
                return (false, "", error);
        }

        product.ProductName = model.ProductName.Trim();
        product.Model = model.Model.Trim();
        product.Unit = model.Unit.Trim();
        product.CategoryId = model.CategoryId;
        product.BrandId = model.BrandId;

        product.Specifications =
            string.IsNullOrWhiteSpace(model.Specifications)
                ? null
                : model.Specifications.Trim();

        product.LengthCm = model.LengthCm;
        product.WidthCm = model.WidthCm;
        product.HeightCm = model.HeightCm;
        product.GrossWeightKg = model.GrossWeightKg;
        product.UpdatedAt = DateTime.UtcNow;

        try
        {
            await repository.SaveChangesAsync();

            return (true, null, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "",
                "Sản phẩm vừa được thay đổi hoặc xóa. Hãy tải lại trang.");
        }
    }
}