using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using UserEntity = PRN222_EWMS_Web.Models.User;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Warehouse;

public class OutboundRepository(EwmsDbContext db) : IOutboundRepository
{
    public IQueryable<UserEntity> Users => db.Users;
    public IQueryable<WarehouseEntity> Warehouses => db.Warehouses;
    public IQueryable<Outbound> Outbounds => db.Outbounds;
    public IQueryable<OutboundItem> Items => db.OutboundItems;
    public IQueryable<SalesOrder> Orders => db.SalesOrders;
    public IQueryable<PickingClaim> Claims => db.PickingClaims;
    public IQueryable<SerialMovement> Movements => db.SerialMovements;
    public IQueryable<StorageLocation> Locations => db.StorageLocations;

    public Task<SalesOrder?> OrderForUpdateAsync(long id) => db.SalesOrders
        .FromSqlInterpolated($"SELECT * FROM dbo.SalesOrder WITH (UPDLOCK,HOLDLOCK) WHERE SalesOrderID={id}")
        .Include(o => o.Customer).Include(o => o.Warehouse)
        .Include(o => o.SalesOrderLines).ThenInclude(l => l.Product).AsSplitQuery().SingleOrDefaultAsync();

    public Task<Outbound?> OutboundForUpdateAsync(long id) => db.Outbounds
        .FromSqlInterpolated($"SELECT * FROM dbo.Outbound WITH (UPDLOCK,HOLDLOCK) WHERE OutboundID={id}")
        .Include(o => o.SalesOrder).SingleOrDefaultAsync();

    public Task<SerialItem?> SerialForUpdateAsync(string serial) => db.SerialItems
        .FromSqlInterpolated($"SELECT * FROM dbo.SerialItem WITH (UPDLOCK,HOLDLOCK) WHERE SerialNumber={serial}")
        .SingleOrDefaultAsync();

    public Task<SupplySource?> SourceForUpdateAsync(long id) => db.SupplySources
        .FromSqlInterpolated($"SELECT * FROM dbo.SupplySource WITH (UPDLOCK,HOLDLOCK) WHERE SupplySourceID={id}")
        .SingleOrDefaultAsync();

    public Task<List<Reservation>> ReservationsForUpdateAsync(long orderId) => db.Reservations
        .FromSqlInterpolated($"SELECT * FROM dbo.Reservation WITH (UPDLOCK,HOLDLOCK) WHERE SalesOrderID={orderId}")
        .Where(r => r.Status == "ACTIVE" && r.SupplyState == "PHYSICAL")
        .OrderBy(r => r.CreatedAt).ThenBy(r => r.ReservationId).ToListAsync();

    public Task<List<OutboundItem>> GetItemsAsync(long id, bool tracked)
    {
        var query = db.OutboundItems.Where(i => i.OutboundId == id);
        if (!tracked) query = query.AsNoTracking();
        return query.Include(i => i.SerialItem).ThenInclude(s => s.Product)
            .Include(i => i.SourceAtIssue).ThenInclude(s => s.Inbound)
            .Include(i => i.SourceAtIssue).ThenInclude(s => s.ReturnReceipt)
            .Include(i => i.SourceAtIssue).ThenInclude(s => s.TransferReceipt)
            .Include(i => i.SourceAtIssue).ThenInclude(s => s.Adjustment)
            .AsSplitQuery().OrderByDescending(i => i.ScannedAt).ToListAsync();
    }

    public async Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> work)
    {
        await db.Database.OpenConnectionAsync();
        object? previousActor = null;
        var changed = false;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT SESSION_CONTEXT(N'ActorUserID')";
                previousActor = await command.ExecuteScalarAsync();
            }
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}");
            changed = true;
            var result = await work();
            await RestoreActorAsync(previousActor);
            changed = false;
            await transaction.CommitAsync();
            return result;
        }
        finally
        {
            try
            {
                if (changed) await RestoreActorAsync(previousActor);
            }
            catch
            {
                if (db.Database.GetDbConnection() is SqlConnection connection) SqlConnection.ClearPool(connection);
                throw;
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    private Task RestoreActorAsync(object? actor)
    {
        var parameter = new SqlParameter("@previousActor", SqlDbType.Variant) { Value = actor ?? DBNull.Value };
        return db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=@previousActor", parameter);
    }

    public Task SaveAsync() => db.SaveChangesAsync();
    public void Touch(Outbound outbound) => db.Entry(outbound).Property(o => o.Note).IsModified = true;
    public void Add(Outbound outbound) => db.Outbounds.Add(outbound);
    public void Add(OutboundItem item) => db.OutboundItems.Add(item);
    public void Add(PickingClaim claim) => db.PickingClaims.Add(claim);
    public void Add(DemandAllocation allocation) => db.DemandAllocations.Add(allocation);
    public void Add(SerialMovement movement) => db.SerialMovements.Add(movement);
    public void Remove(PickingClaim claim) => db.PickingClaims.Remove(claim);
}
