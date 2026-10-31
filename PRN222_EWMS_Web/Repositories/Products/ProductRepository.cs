using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Products;

public class ProductRepository(EwmsDbContext db)
    : IProductRepository
{
    public Task<int?> GetWarehouseIdAsync(int userId)
    {
        return db.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.WarehouseId)
            .FirstOrDefaultAsync();
    }

    private IQueryable<Product> Query(int? warehouseId)
    {
        var query = db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(p =>
                p.SerialItems.Any(s =>
                    s.CurrentWarehouseId == warehouseId.Value));
        }

        return query;
    }

    public async Task<List<Product>> GetListAsync(
        string? keyword, int? warehouseId)
    {
        var query = Query(warehouseId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();

            query = query.Where(p =>
                p.ProductCode.Contains(keyword) ||
                p.ProductName.Contains(keyword) ||
                p.Model.Contains(keyword));
        }

        return await query
            .OrderBy(p => p.ProductCode)
            .ToListAsync();
    }

    public Task<Product?> GetDetailAsync(
        int id, int? warehouseId)
    {
        return Query(warehouseId)
            .FirstOrDefaultAsync(p => p.ProductId == id);
    }

    public Task<List<Category>> GetCategoriesAsync()
    {
        return db.Categories
            .AsNoTracking()
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public Task<List<Brand>> GetBrandsAsync()
    {
        return db.Brands
            .AsNoTracking()
            .OrderBy(b => b.BrandName)
            .ToListAsync();
    }

    public Task<bool> ProductCodeExistsAsync(string code)
    {
        return db.Products.AnyAsync(p => p.ProductCode == code);
    }

    public Task<bool> CategoryExistsAsync(int id)
    {
        return db.Categories.AnyAsync(c => c.CategoryId == id);
    }

    public Task<bool> BrandExistsAsync(int id)
    {
        return db.Brands.AnyAsync(b => b.BrandId == id);
    }

    public async Task<int> CreateAsync(Product product)
    {
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product.ProductId;
    }

    public Task<Product?> GetForEditAsync(int id)
    {
        // Không AsNoTracking vì entity sẽ được cập nhật.
        return db.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);
    }

    public Task<bool> HasItemsInWarehouseAsync(int productId)
    {
        return db.SerialItems.AnyAsync(s =>
            s.ProductId == productId &&
            s.CurrentWarehouseId != null);
    }

    public Task SaveChangesAsync()
    {
        return db.SaveChangesAsync();
    }

    public async Task<string?> GetZoneRuleErrorAsync(
    int productId, int categoryId, int brandId)
    {
        var positions = await db.SerialItems
            .AsNoTracking()
            .Where(s =>
                s.ProductId == productId &&
                s.CurrentWarehouseId != null)
            .Select(s => new
            {
                s.CurrentWarehouseId,
                s.CurrentLocationId
            })
            .Distinct()
            .ToListAsync();

        if (positions.Count == 0)
            return null;

        if (positions.Any(p => p.CurrentLocationId == null))
        {
            return "Sản phẩm có hàng trong kho chưa được gán vị trí. " +
                   "Chưa thể kiểm tra thay đổi danh mục/thương hiệu.";
        }

        var warehouseIds = positions
            .Select(p => p.CurrentWarehouseId!.Value)
            .Distinct()
            .ToList();

        var locations = await db.StorageLocations
            .AsNoTracking()
            .Where(l => warehouseIds.Contains(l.WarehouseId))
            .ToDictionaryAsync(l => l.LocationId);

        var zones = new Dictionary<int, StorageLocation>();

        foreach (var position in positions)
        {
            int? currentId = position.CurrentLocationId;
            var visited = new HashSet<int>();
            StorageLocation? zone = null;

            while (currentId.HasValue)
            {
                if (!visited.Add(currentId.Value) ||
                    !locations.TryGetValue(currentId.Value, out var location) ||
                    location.WarehouseId != position.CurrentWarehouseId)
                {
                    return "Cấu trúc vị trí lưu trữ không hợp lệ. " +
                           "Không thể kiểm tra danh mục/thương hiệu.";
                }

                if (location.LocationType == "ZONE")
                {
                    zone = location;
                    break;
                }

                currentId = location.ParentLocationId;
            }

            if (zone is null)
            {
                return "Không tìm thấy zone của vị trí đang chứa sản phẩm.";
            }

            zones[zone.LocationId] = zone;
        }

        var zoneIds = zones.Keys.ToList();

        var rules = await db.ZoneStorageRules
            .AsNoTracking()
            .Where(r => zoneIds.Contains(r.ZoneLocationId))
            .ToListAsync();

        foreach (var zone in zones.Values)
        {
            if (zone.ZoneType == "RESERVE")
                continue;

            if (zone.ZoneType != "STANDARD")
            {
                return $"Zone '{zone.LocationName}' có loại không hợp lệ.";
            }

            bool allowed = rules.Any(r =>
                r.ZoneLocationId == zone.LocationId &&
                r.CategoryId == categoryId &&
                (r.BrandId == null || r.BrandId == brandId));

            if (!allowed)
            {
                return $"Danh mục/thương hiệu mới không phù hợp với " +
                       $"quy định của zone '{zone.LocationName}' " +
                       $"(kho ID {zone.WarehouseId}).";
            }
        }

        return null;
    }
}