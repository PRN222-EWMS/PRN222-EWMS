using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public interface IPutawayService
{
    Task<PutawayIndexViewModel> GetIndexAsync(int userId, PutawayFilter filter);
    Task<PutawaySerialViewModel> LookupAsync(int userId, string serial);
    Task<PutawayResult> ConfirmAsync(int userId, ConfirmPutawayInput input);
}
