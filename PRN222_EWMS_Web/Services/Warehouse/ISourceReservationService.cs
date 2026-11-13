using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Services.Warehouse;

public interface ISourceReservationService
{
    Task LockWarehouseAsync(int warehouseId, CancellationToken ct = default);
    Task<string?> GetReceivingBlockReasonAsync(int warehouseId, CancellationToken ct = default);
    Task<string?> GetStocktakeBlockReasonAsync(int warehouseId, IReadOnlyCollection<int?> locationIds, CancellationToken ct = default);
    Task<List<SalesSourceOption>> GetOptionsAsync(int warehouseId, DateOnly? requestedDate, long? orderId = null, CancellationToken ct = default);
    Task<long> ResolveSourceAsync(int warehouseId, SalesSourceChoiceInput choice, CancellationToken ct = default);
    Task ReservePlansAsync(SalesOrder order, CancellationToken ct = default);
    Task AfterInboundAsync(long inboundId, CancellationToken ct = default);
    Task AfterDispatchAsync(long transferOrderId, CancellationToken ct = default);
    Task AfterTransferReceiptAsync(long receiptId, CancellationToken ct = default);
    Task AfterTransferLossAppliedAsync(long lossRequestId, CancellationToken ct = default);
    Task ReconcileWarehouseAsync(int warehouseId, string reason, CancellationToken ct = default);
}
