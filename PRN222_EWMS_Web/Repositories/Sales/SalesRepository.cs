using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Repositories.Sales;

public class SalesRepository(EwmsDbContext db) : ISalesRepository
{
    public IQueryable<SalesOrder> Orders => db.SalesOrders.AsNoTracking();
    public IQueryable<Customer> Customers => db.Customers.AsNoTracking();
    public IQueryable<WarehouseEntity> Warehouses => db.Warehouses.AsNoTracking();
    public IQueryable<Product> Products => db.Products.AsNoTracking();
    public IQueryable<SupplySource> SupplySources => db.SupplySources.AsNoTracking();
    public IQueryable<Reservation> Reservations => db.Reservations.AsNoTracking();

    public Task<Customer?> FindCustomerAsync(int id) => db.Customers.SingleOrDefaultAsync(x => x.CustomerId == id);

    public async Task<int> AddCustomerAsync(Customer customer, int actorId)
    {
        db.Customers.Add(customer);
        await SaveChangesWithActorAsync(actorId);
        return customer.CustomerId;
    }

    public Task SaveCustomerAsync(int actorId) => SaveChangesWithActorAsync(actorId);

    public Task<SalesOrder?> FindOrderForEditAsync(long id)
    {
        return db.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.SalesOrderLines)
                .ThenInclude(l => l.Product)
            .Include(o => o.Outbounds)
            .Include(o => o.Reservations)
            .SingleOrDefaultAsync(o => o.SalesOrderId == id);
    }

    public Task<bool> HasAnyOutboundAsync(long salesOrderId)
    {
        return db.Outbounds.AsNoTracking().AnyAsync(o => o.SalesOrderId == salesOrderId);
    }

    public async Task<string> GenerateNextSoCodeAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SO-{year}-";

        var latestCode = await db.SalesOrders
            .Where(p => p.Socode.StartsWith(prefix))
            .OrderByDescending(p => p.Socode)
            .Select(p => p.Socode)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(latestCode) && latestCode.Length > prefix.Length)
        {
            if (int.TryParse(latestCode[prefix.Length..], out var seq))
            {
                nextSeq = seq + 1;
            }
        }
        return $"{prefix}{nextSeq:D3}";
    }

    public async Task AddOrderAsync(SalesOrder order)
    {
        await db.SalesOrders.AddAsync(order);
    }

    public Task RemoveOrderLinesAsync(IEnumerable<SalesOrderLine> lines)
    {
        db.SalesOrderLines.RemoveRange(lines);
        return Task.CompletedTask;
    }

    public async Task CloseActiveReservationsAsync(long salesOrderId)
    {
        var activeReservations = await db.Reservations
            .Where(r => r.SalesOrderId == salesOrderId && r.Status == "ACTIVE")
            .ToListAsync();

        foreach (var r in activeReservations)
        {
            r.Status = "CLOSED";
            r.ReleasedQuantity = r.ReservedQuantity - r.ConsumedQuantity;
            r.ClosedAt = DateTime.UtcNow;
        }
    }

    public async Task SaveChangesWithActorAsync(int actorId)
    {
        if (actorId <= 0) throw new InvalidOperationException("Authenticated user required.");
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

    public async Task<List<ProductStockLookupDto>> GetAvailableProductsAsync(int warehouseId)
    {
        var heldBySource = await db.Reservations
            .Where(r => r.Status == "ACTIVE" && r.WarehouseId == warehouseId)
            .GroupBy(r => r.SupplySourceId)
            .Select(g => new { SupplySourceId = g.Key, ReservedBalance = g.Sum(r => (long)r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity) })
            .ToDictionaryAsync(x => x.SupplySourceId, x => x.ReservedBalance);

        var sources = await db.SupplySources
            .Where(s => s.WarehouseId == warehouseId && s.ItemCondition == "GOOD")
            .Select(s => new { s.SupplySourceId, s.ProductId, s.EligibleQuantity })
            .ToListAsync();

        var lookup = sources
            .Select(s =>
            {
                var reserved = heldBySource.TryGetValue(s.SupplySourceId, out var b) ? b : 0;
                return new { s.ProductId, Unreserved = Math.Max(0, (long)s.EligibleQuantity - reserved) };
            })
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Unreserved));

        var products = await db.Products
            .Where(p => p.Status == "ACTIVE")
            .OrderBy(p => p.ProductCode)
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.Unit })
            .ToListAsync();

        return products.Select(p => new ProductStockLookupDto
        {
            ProductId = p.ProductId,
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            Unit = p.Unit ?? "",
            WarehouseId = warehouseId,
            UnreservedQuantity = lookup.TryGetValue(p.ProductId, out var unreserved) ? unreserved : 0
        }).ToList();
    }
}


