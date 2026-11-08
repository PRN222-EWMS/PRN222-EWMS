using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public interface IPutawayRepository
{
    Task<PutawayActor?> GetActorAsync(int userId, CancellationToken ct);
    Task<PutawayQueueViewModel> GetQueueAsync(int warehouseId, PutawayFilter filter, CancellationToken ct);
    Task<SerialItem?> GetSerialAsync(int warehouseId, long? id, string? serial, bool tracked, CancellationToken ct);
    Task<PutawayLayout?> GetLayoutAsync(int warehouseId, CancellationToken ct);
    Task<bool> HasPickingClaimAsync(long serialId, CancellationToken ct);
    Task<List<PutawayOccupant>> GetOccupantsAsync(int warehouseId, int? locationId, CancellationToken ct);
    Task<T> InTransactionAsync<T>(int userId, int warehouseId, Func<CancellationToken, Task<T>> work, CancellationToken ct);
    Task LockSerialAsync(int warehouseId, long serialId, CancellationToken ct);
    Task SavePlacementAsync(SerialItem serial, int locationId, int userId, byte[] version, string path, CancellationToken ct);
}
