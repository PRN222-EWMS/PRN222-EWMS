using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public interface IInboundRepository
{
    Task<int?> GetWarehouseIdAsync(int userId);
    Task<List<Inbound>> ListAsync(int warehouseId, string? search, string? status, DateOnly? from, DateOnly? to, int skip, int take);
    Task<int> CountAsync(int warehouseId, string? search, string? status, DateOnly? from, DateOnly? to);
    Task<List<PurchaseOrder>> GetEligibleOrdersAsync(int warehouseId);
    Task<PurchaseOrder?> GetOrderAsync(long id, int warehouseId);
    Task<Inbound?> GetInboundAsync(long id, int warehouseId);
    Task<Inbound?> GetByCodeAsync(string code, int warehouseId);
    Task<Inbound?> GetDraftForOrderAsync(long orderId);
    Task<List<InboundItem>> GetOrderItemsAsync(long orderId);
    Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds);
    Task<bool> SerialExistsAsync(string serial, long? excludingItemId = null);
    Task<InboundItem?> GetItemAsync(long id, long inboundId);
    Task<SupplySource?> GetIncomingSourceAsync(long lineId);
    Task<List<SerialItem>> GetPendingPutawayAsync(int warehouseId);
    Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> work);
    Task SaveAsync();
    void TouchInbound(Inbound inbound);
    void AddInbound(Inbound inbound);
    void AddItem(InboundItem item);
    void AddSerial(SerialItem item);
    void AddMovement(SerialMovement movement);
    void AddSource(SupplySource source);
}
