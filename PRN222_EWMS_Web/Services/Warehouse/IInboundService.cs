using PRN222_EWMS_Web.ViewModels.Inbound;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public interface IInboundService
{
    Task<InboundListViewModel> ListAsync(int userId, string? search, string? status, DateOnly? from, DateOnly? to, int page);
    Task<InboundCreateViewModel> GetCreateAsync(int userId, long? orderId = null);
    Task<InboundDetailViewModel?> GetDetailAsync(int userId, long id);
    Task<long> CreateDraftAsync(int userId, InboundCreateViewModel input);
    Task AddItemAsync(int userId, long inboundId, ReceiveSerialInput input);
    Task UpdateItemAsync(int userId, long inboundId, long itemId, ReceiveSerialInput input);
    Task RemoveItemAsync(int userId, long inboundId, long itemId, string version);
    Task CompleteAsync(int userId, long inboundId, string version);
    Task CancelAsync(int userId, long inboundId, string version, string reason);
    Task<InboundPendingPutawayViewModel> GetPendingPutawayAsync(int userId);
}
