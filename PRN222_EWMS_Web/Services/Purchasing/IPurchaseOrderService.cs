using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Services.Purchasing;

public interface IPurchaseOrderService
{
    Task<PoListViewModel> GetOrdersAsync(PoListViewModel filter, int currentUserId, string role);
    Task<PoDetailsViewModel?> GetOrderDetailsAsync(long id, int currentUserId, string role);
    Task<PoFormViewModel> GetCreateFormAsync(int currentUserId);
    Task<PoFormViewModel?> GetEditFormAsync(long id, int currentUserId, string role);
    Task<(bool Success, string Message, long PoId)> CreateOrderAsync(PoFormViewModel model, bool confirmNow, int actorId);
    Task<(bool Success, string Message)> UpdateOrderAsync(long id, PoFormViewModel model, int actorId, string role);
    Task<(bool Success, string Message)> ConfirmOrderAsync(long id, int actorId, string role);
    Task<(bool Success, string Message)> CloseRemainderAsync(long id, string reason, int actorId, string role);
    Task<List<ProductLookupDto>> GetProductsBySupplierAsync(int supplierId);
}
