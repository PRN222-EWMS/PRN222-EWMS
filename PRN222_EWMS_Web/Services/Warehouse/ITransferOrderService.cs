using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface ITransferOrderService
{
    Task<TransferListViewModel> GetListAsync(
        int userId,
        string role,
        string? search = null,
        string? status = null,
        int? fromWarehouseId = null,
        int? toWarehouseId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int page = 1,
        int pageSize = 10);

    Task<TransferDetailsViewModel?> GetDetailsAsync(long id, int userId, string role);

    Task<CreateTransferOrderViewModel> GetCreateFormAsync();

    Task<(bool Success, string? Error, long TransferOrderId)> CreateTransferOrderAsync(
        CreateTransferOrderViewModel model,
        int userId);

    Task<EditTransferOrderViewModel?> GetEditFormAsync(long id, int userId);

    Task<(bool Success, string? Error)> UpdateTransferOrderAsync(
        EditTransferOrderViewModel model,
        int userId);

    Task<(bool Success, string? Error)> CancelTransferOrderAsync(
        long id,
        int userId);
}
