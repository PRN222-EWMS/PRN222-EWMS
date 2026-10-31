using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PRN222_EWMS_Web.Models;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Purchasing;

public class PurchaseOrderRepository(EwmsDbContext db) : IPurchaseOrderRepository
{
    public IQueryable<PurchaseOrder> Orders => db.PurchaseOrders.AsNoTracking();
    public IQueryable<VwPurchaseOrderProgress> Progress => db.VwPurchaseOrderProgresses.AsNoTracking();
    public IQueryable<Models.Supplier> Suppliers => db.Suppliers.AsNoTracking();
    public IQueryable<WarehouseEntity> Warehouses => db.Warehouses.AsNoTracking();
    public IQueryable<Product> Products => db.Products.AsNoTracking();
    public IQueryable<SupplierProduct> SupplierProducts => db.SupplierProducts.AsNoTracking();

    public async Task<PurchaseOrder?> FindByIdAsync(long id, bool asNoTracking = false)
    {
        var query = asNoTracking ? db.PurchaseOrders.AsNoTracking() : db.PurchaseOrders;
        return await query
            .Include(p => p.PurchaseOrderLines)
            .SingleOrDefaultAsync(p => p.PurchaseOrderId == id);
    }

    public async Task<PurchaseOrder?> GetDetailsAsync(long id)
    {
        return await db.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .Include(p => p.PurchaseOrderLines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(prod => prod.Category)
            .Include(p => p.PurchaseOrderLines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(prod => prod.Brand)
            .Include(p => p.Inbounds)
                .ThenInclude(i => i.PerformedByUser)
            .Include(p => p.Inbounds)
                .ThenInclude(i => i.InboundItemInbounds)
            .SingleOrDefaultAsync(p => p.PurchaseOrderId == id);
    }

    public async Task<bool> HasAnyInboundAsync(long purchaseOrderId)
    {
        return await db.Inbounds
            .AsNoTracking()
            .AnyAsync(i => i.PurchaseOrderId == purchaseOrderId);
    }

    public async Task<string> GenerateNextPoCodeAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PO-{year}-";
        
        var latestCode = await db.PurchaseOrders
            .Where(p => p.Pocode.StartsWith(prefix))
            .OrderByDescending(p => p.Pocode)
            .Select(p => p.Pocode)
            .FirstOrDefaultAsync();

        if (latestCode != null && int.TryParse(latestCode.Replace(prefix, ""), out int lastSeq))
        {
            return $"{prefix}{(lastSeq + 1):D3}";
        }

        return $"{prefix}001";
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await db.Database.BeginTransactionAsync();
    }

    public async Task AddAsync(PurchaseOrder po, int actorId)
    {
        db.PurchaseOrders.Add(po);
        await SaveChangesWithActorAsync(actorId);
    }

    public async Task UpdateAsync(PurchaseOrder po, int actorId)
    {
        // Entity is tracked, just save changes with actor
        await SaveChangesWithActorAsync(actorId);
    }

    public async Task SaveChangesWithActorAsync(int actorId)
    {
        if (actorId <= 0) throw new InvalidOperationException("Yêu cầu thông tin tài khoản người dùng hợp lệ.");
        
        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}");
            await db.SaveChangesAsync();
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL"); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
