using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public interface IPutawayService
{
    Task<PutawayIndexViewModel> GetIndexAsync(int userId, PutawayFilter filter, CancellationToken ct);
    Task<PutawaySerialViewModel> LookupAsync(int userId, string serial, CancellationToken ct);
    Task<PutawayResult> ConfirmAsync(int userId, ConfirmPutawayInput input, CancellationToken ct);
}
