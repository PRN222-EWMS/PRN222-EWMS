using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface IOutboundService
{
    Task<OutboundListViewModel> GetListAsync(
        int userId,
        string role,
        string? search,
        string? status,
        int? warehouseId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page = 1,
        int pageSize = 10);

    Task<OutboundDetailsViewModel?> GetDetailsAsync(long id, int userId, string role);

    // Chức năng 70: Create Outbound Receipt & Picking Workbench
    Task<List<EligibleSalesOrderDto>> GetEligibleSalesOrdersAsync(int userId);
    Task<(bool Success, string? Error, long OutboundId)> StartPickingSessionAsync(long salesOrderId, int userId);
    Task<PickingWorkbenchViewModel?> GetPickingWorkbenchAsync(long outboundId, int userId);
    Task<(bool Success, string? Error)> ScanSerialAsync(long outboundId, string serialNumber, int userId);
    Task<(bool Success, string? Error)> RemoveSerialAsync(long outboundId, long serialItemId, int userId);
    Task<(bool Success, string? Error)> MarkReadyAsync(long outboundId, int userId);
    Task<(bool Success, string? Error)> CancelPickingAsync(long outboundId, int userId);
}

