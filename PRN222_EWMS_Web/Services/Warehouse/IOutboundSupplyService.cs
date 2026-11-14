using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface IOutboundSupplyService
{
    Task LockWarehouseAsync(int warehouseId);
    Task<string?> GetReceivingBlockReasonAsync(int warehouseId);
    Task ReconcileWarehouseAsync(int warehouseId);
    Task AfterInboundPostedAsync(long inboundId);
    Task<OutboundSupplySnapshot> GetAvailabilityAsync(long salesOrderId, long? outboundId = null);
    Task EnsureSourceReservationAsync(long salesOrderId, long lineId, long sourceId, int minimumBalance);
    Task RefreshShortageAsync(long salesOrderId);
}
