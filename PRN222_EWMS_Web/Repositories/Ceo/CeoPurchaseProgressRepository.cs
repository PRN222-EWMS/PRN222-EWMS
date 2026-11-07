using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Repositories.Ceo;

public class CeoPurchaseProgressRepository(EwmsDbContext db) : ICeoPurchaseProgressRepository
{
    private IQueryable<PurchaseOrder> Filter(CeoPurchaseProgressFilter filter)
    {
        var query = db.PurchaseOrders.AsNoTracking();
        if (filter.Search is not null) query = query.Where(po => po.Pocode.Contains(filter.Search));
        if (filter.Status is not null) query = query.Where(po => po.Status == filter.Status);
        if (filter.WarehouseId.HasValue) query = query.Where(po => po.WarehouseId == filter.WarehouseId.Value);
        if (filter.SupplierId.HasValue) query = query.Where(po => po.SupplierId == filter.SupplierId.Value);
        return query;
    }

    public Task<int> CountAsync(CeoPurchaseProgressFilter filter) => Filter(filter).CountAsync();

    public Task<List<PurchaseOrder>> GetPageAsync(CeoPurchaseProgressFilter filter, int page, int pageSize) =>
        Filter(filter).Include(po => po.Supplier).Include(po => po.Warehouse).Include(po => po.PurchaseOrderLines)
            .OrderByDescending(po => po.OrderDate).ThenByDescending(po => po.PurchaseOrderId)
            .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();

    public Task<PurchaseOrder?> GetOrderAsync(long id) => db.PurchaseOrders.AsNoTracking()
        .Include(po => po.Supplier).Include(po => po.Warehouse)
        .Include(po => po.PurchaseOrderLines).ThenInclude(line => line.Product)
        .SingleOrDefaultAsync(po => po.PurchaseOrderId == id);

    public Task<List<VwPurchaseOrderProgress>> GetProgressAsync(IEnumerable<long> orderIds)
    {
        var ids = orderIds.Distinct().ToArray();
        return db.VwPurchaseOrderProgresses.AsNoTracking().Where(row => ids.Contains(row.PurchaseOrderId)).ToListAsync();
    }

    public Task<List<CeoLookupOption>> GetWarehousesAsync() => db.Warehouses.AsNoTracking()
        .OrderBy(warehouse => warehouse.WarehouseName)
        .Select(warehouse => new CeoLookupOption(warehouse.WarehouseId, warehouse.WarehouseName)).ToListAsync();

    public Task<List<CeoLookupOption>> GetSuppliersAsync() => db.Suppliers.AsNoTracking()
        .OrderBy(supplier => supplier.SupplierName)
        .Select(supplier => new CeoLookupOption(supplier.SupplierId, supplier.SupplierName)).ToListAsync();
}
