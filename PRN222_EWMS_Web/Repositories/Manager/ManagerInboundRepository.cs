using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public class ManagerInboundRepository(EwmsDbContext db) : IManagerInboundRepository
{
    private IQueryable<Inbound> Filter(ManagerInboundFilter filter)
    {
        var query = db.Inbounds.AsNoTracking();
        if (filter.Search is not null) query = query.Where(receipt => receipt.ReceiptCode.Contains(filter.Search)
            || receipt.PurchaseOrder.Pocode.Contains(filter.Search));
        if (filter.Status is not null) query = query.Where(receipt => receipt.Status == filter.Status);
        if (filter.WarehouseId.HasValue) query = query.Where(receipt => receipt.PurchaseOrder.WarehouseId == filter.WarehouseId.Value);
        if (filter.FromDate.HasValue) query = query.Where(receipt => receipt.ReceiptDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(receipt => receipt.ReceiptDate <= filter.ToDate.Value);
        return query;
    }

    public Task<int> CountAsync(ManagerInboundFilter filter) => Filter(filter).CountAsync();

    public Task<List<Inbound>> GetPageAsync(ManagerInboundFilter filter, int page, int pageSize) => Filter(filter)
        .Include(receipt => receipt.PurchaseOrder).ThenInclude(po => po.Supplier)
        .Include(receipt => receipt.PurchaseOrder).ThenInclude(po => po.Warehouse)
        .Include(receipt => receipt.PurchaseOrder).ThenInclude(po => po.PurchaseOrderLines)
        .Include(receipt => receipt.InboundItemInbounds)
        .OrderByDescending(receipt => receipt.CreatedAt).ThenByDescending(receipt => receipt.InboundId)
        .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();

    public Task<Inbound?> GetDetailAsync(long id) => db.Inbounds.AsNoTracking()
        .Include(receipt => receipt.PurchaseOrder).ThenInclude(po => po.Supplier)
        .Include(receipt => receipt.PurchaseOrder).ThenInclude(po => po.Warehouse)
        .Include(receipt => receipt.PurchaseOrder).ThenInclude(po => po.PurchaseOrderLines).ThenInclude(line => line.Product)
        .Include(receipt => receipt.CreatedByUser).Include(receipt => receipt.PerformedByUser)
        .Include(receipt => receipt.InboundItemInbounds).ThenInclude(item => item.Poline).ThenInclude(line => line.Product)
        .AsSplitQuery().SingleOrDefaultAsync(receipt => receipt.InboundId == id);

    public Task<List<VwPurchaseOrderProgress>> GetPoProgressAsync(IEnumerable<long> poIds)
    {
        var ids = poIds.Distinct().ToArray();
        return db.VwPurchaseOrderProgresses.AsNoTracking().Where(row => ids.Contains(row.PurchaseOrderId)).ToListAsync();
    }

    public Task<List<ManagerWarehouseOption>> GetWarehousesAsync() => db.Warehouses.AsNoTracking()
        .OrderBy(warehouse => warehouse.WarehouseName)
        .Select(warehouse => new ManagerWarehouseOption(warehouse.WarehouseId, warehouse.WarehouseName)).ToListAsync();
}
