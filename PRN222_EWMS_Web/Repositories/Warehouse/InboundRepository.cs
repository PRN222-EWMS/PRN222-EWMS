using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public class InboundRepository(EwmsDbContext db) : IInboundRepository
{
    public Task<int?> GetWarehouseIdAsync(int userId) => db.Users.AsNoTracking()
        .Where(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF")
        .Select(u => u.WarehouseId).SingleOrDefaultAsync();

    private IQueryable<Inbound> Filter(int warehouseId, string? search, string? status, DateOnly? from, DateOnly? to)
    {
        var query = db.Inbounds.AsNoTracking().Where(i => i.PurchaseOrder.WarehouseId == warehouseId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(i => i.ReceiptCode.Contains(search) || i.PurchaseOrder.Pocode.Contains(search));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(i => i.Status == status);
        if (from.HasValue) query = query.Where(i => i.ReceiptDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.ReceiptDate <= to.Value);
        return query;
    }

    public Task<int> CountAsync(int warehouseId, string? search, string? status, DateOnly? from, DateOnly? to) =>
        Filter(warehouseId, search, status, from, to).CountAsync();

    public Task<List<Inbound>> ListAsync(int warehouseId, string? search, string? status, DateOnly? from, DateOnly? to, int skip, int take) =>
        Filter(warehouseId, search, status, from, to)
            .Include(i => i.PurchaseOrder).ThenInclude(po => po.Supplier)
            .Include(i => i.PurchaseOrder).ThenInclude(po => po.Warehouse)
            .Include(i => i.PurchaseOrder).ThenInclude(po => po.PurchaseOrderLines)
            .Include(i => i.CreatedByUser).Include(i => i.InboundItemInbounds)
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.InboundId).Skip(skip).Take(take).AsSplitQuery().ToListAsync();

    public Task<List<PurchaseOrder>> GetEligibleOrdersAsync(int warehouseId) => db.PurchaseOrders.AsNoTracking()
        .Include(po => po.Supplier).Include(po => po.Warehouse).Include(po => po.PurchaseOrderLines)
        .Where(po => po.WarehouseId == warehouseId && (po.Status == "CONFIRMED" || po.Status == "PARTIALLY_RECEIVED"))
        .OrderByDescending(po => po.CreatedAt).ToListAsync();

    public Task<PurchaseOrder?> GetOrderAsync(long id, int warehouseId) => db.PurchaseOrders
        .Include(po => po.Supplier).Include(po => po.Warehouse)
        .Include(po => po.PurchaseOrderLines).ThenInclude(line => line.Product)
        .SingleOrDefaultAsync(po => po.PurchaseOrderId == id && po.WarehouseId == warehouseId);

    public Task<Inbound?> GetInboundAsync(long id, int warehouseId) => db.Inbounds
        .Include(i => i.PurchaseOrder).ThenInclude(po => po.Supplier)
        .Include(i => i.PurchaseOrder).ThenInclude(po => po.Warehouse)
        .Include(i => i.PurchaseOrder).ThenInclude(po => po.PurchaseOrderLines).ThenInclude(line => line.Product)
        .Include(i => i.CreatedByUser).Include(i => i.PerformedByUser)
        .Include(i => i.InboundItemInbounds).ThenInclude(item => item.Poline).ThenInclude(line => line.Product)
        .AsSplitQuery().SingleOrDefaultAsync(i => i.InboundId == id && i.PurchaseOrder.WarehouseId == warehouseId);

    public Task<Inbound?> GetByCodeAsync(string code, int warehouseId) => db.Inbounds
        .SingleOrDefaultAsync(i => i.ReceiptCode == code && i.PurchaseOrder.WarehouseId == warehouseId);

    public Task<Inbound?> GetDraftForOrderAsync(long orderId) => db.Inbounds
        .FirstOrDefaultAsync(i => i.PurchaseOrderId == orderId && i.Status == "DRAFT");

    public Task<List<InboundItem>> GetOrderItemsAsync(long orderId) => db.InboundItems.AsNoTracking()
        .Include(item => item.Inbound)
        .Where(item => item.PurchaseOrderId == orderId && item.IsActive).ToListAsync();

    public Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds) => db.Users.AsNoTracking()
        .Where(user => userIds.Contains(user.UserId)).ToDictionaryAsync(user => user.UserId, user => user.FullName);

    public async Task<bool> SerialExistsAsync(string serial, long? excludingItemId = null) =>
        await db.InboundItems.AnyAsync(item => item.ScannedSerial == serial && item.IsActive && item.InboundItemId != excludingItemId)
        || await db.SerialItems.AnyAsync(item => item.SerialNumber == serial);

    public Task<InboundItem?> GetItemAsync(long id, long inboundId) => db.InboundItems
        .SingleOrDefaultAsync(item => item.InboundItemId == id && item.InboundId == inboundId);

    public Task<SupplySource?> GetIncomingSourceAsync(long lineId) => db.SupplySources
        .SingleOrDefaultAsync(source => source.PolineId == lineId && source.SupplyState == "INCOMING");

    public Task<List<SerialItem>> GetPendingPutawayAsync(int warehouseId) => db.SerialItems.AsNoTracking()
        .Include(item => item.Product).Include(item => item.OriginalInbound)
        .Include(item => item.CurrentWarehouse)
        .Where(item => item.CurrentWarehouseId == warehouseId && item.OriginalInboundId != null
            && item.OriginalInbound!.Status == "DONE" && item.StockState == "IN_WAREHOUSE" && item.CurrentLocationId == null)
        .OrderByDescending(item => item.RegisteredAt).Take(200).ToListAsync();

    public async Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> work)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}");
            var result = await work();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL"); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    public Task SaveAsync() => db.SaveChangesAsync();
    public void TouchInbound(Inbound inbound) => db.Entry(inbound).Property(x => x.Note).IsModified = true;
    public void AddInbound(Inbound inbound) => db.Inbounds.Add(inbound);
    public void AddItem(InboundItem item) => db.InboundItems.Add(item);
    public void AddSerial(SerialItem item) => db.SerialItems.Add(item);
    public void AddMovement(SerialMovement movement) => db.SerialMovements.Add(movement);
    public void AddSource(SupplySource source) => db.SupplySources.Add(source);
}
