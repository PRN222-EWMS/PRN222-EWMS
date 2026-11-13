using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface IOutboundSupplyService
{
    Task LockWarehouseAsync(int warehouseId, CancellationToken ct = default);
    Task<string?> GetReceivingBlockReasonAsync(int warehouseId, CancellationToken ct = default);
    Task ReconcileWarehouseAsync(int warehouseId, CancellationToken ct = default);
    Task AfterInboundPostedAsync(long inboundId, CancellationToken ct = default);
    Task<OutboundSupplySnapshot> GetAvailabilityAsync(long salesOrderId, long? outboundId = null, CancellationToken ct = default);
    Task EnsureSourceReservationAsync(long salesOrderId, long lineId, long sourceId, int minimumBalance, CancellationToken ct = default);
    Task RefreshShortageAsync(long salesOrderId, CancellationToken ct = default);
}
