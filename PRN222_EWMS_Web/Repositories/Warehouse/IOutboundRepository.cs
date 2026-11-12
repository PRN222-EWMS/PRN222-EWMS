using PRN222_EWMS_Web.Models;
using UserEntity = PRN222_EWMS_Web.Models.User;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Warehouse;

public interface IOutboundRepository
{
    IQueryable<UserEntity> Users { get; }
    IQueryable<WarehouseEntity> Warehouses { get; }
    IQueryable<Outbound> Outbounds { get; }
    IQueryable<OutboundItem> Items { get; }
    IQueryable<SalesOrder> Orders { get; }
    IQueryable<PickingClaim> Claims { get; }
    IQueryable<SerialMovement> Movements { get; }
    IQueryable<StorageLocation> Locations { get; }
    Task<SalesOrder?> OrderForUpdateAsync(long id);
    Task<Outbound?> OutboundForUpdateAsync(long id);
    Task<SerialItem?> SerialForUpdateAsync(string serial);
    Task<SupplySource?> SourceForUpdateAsync(long id);
    Task<List<Reservation>> ReservationsForUpdateAsync(long orderId);
    Task<List<OutboundItem>> GetItemsAsync(long id, bool tracked);
    Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> work);
    Task SaveAsync();
    void Touch(Outbound outbound);
    void Add(Outbound outbound);
    void Add(OutboundItem item);
    void Add(PickingClaim claim);
    void Add(DemandAllocation allocation);
    void Add(SerialMovement movement);
    void Remove(PickingClaim claim);
}
