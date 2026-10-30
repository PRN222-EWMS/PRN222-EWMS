using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Sales;

public class SalesRepository(EwmsDbContext db) : ISalesRepository
{
    public IQueryable<SalesOrder> Orders => db.SalesOrders.AsNoTracking();
    public IQueryable<Customer> Customers => db.Customers.AsNoTracking();
    public IQueryable<Warehouse> Warehouses => db.Warehouses.AsNoTracking();
    public Task<Customer?> FindCustomerAsync(int id) => db.Customers.SingleOrDefaultAsync(x => x.CustomerId == id);

    public async Task<int> AddCustomerAsync(Customer customer, int actorId)
    {
        db.Customers.Add(customer);
        await SaveCustomerAsync(actorId);
        return customer.CustomerId;
    }

    public async Task SaveCustomerAsync(int actorId)
    {
        if (actorId <= 0) throw new InvalidOperationException("Authenticated user required.");
        await db.Database.OpenConnectionAsync();
        try
        {
            // Keep the authenticated actor on the same connection used by audit triggers.
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
