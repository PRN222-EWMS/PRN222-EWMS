using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public interface IPutawayRepository
{
    Task<PutawayActor?> GetActorAsync(int userId);
    Task<PutawayQueueViewModel> GetQueueAsync(int warehouseId, PutawayFilter filter);
    Task<SerialItem?> GetSerialAsync(int warehouseId, long? id, string? serial, bool tracked);
    Task<PutawayLayout?> GetLayoutAsync(int warehouseId);
    Task<bool> HasPickingClaimAsync(long serialId);
    Task<List<PutawayOccupant>> GetOccupantsAsync(int warehouseId, int? locationId);
    Task<T> InTransactionAsync<T>(int userId, int warehouseId, Func<Task<T>> work);
    Task LockSerialAsync(int warehouseId, long serialId);
    Task SavePlacementAsync(SerialItem serial, int locationId, int userId, byte[] version, string path);
}
