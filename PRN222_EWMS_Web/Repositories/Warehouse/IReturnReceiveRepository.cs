using PRN222_EWMS_Web.Models;
namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;
public interface IReturnReceiveRepository
{
 Task<int?> WarehouseAsync(int userId);
 Task<int> CountOrdersAsync(int warehouseId,string? search);
 Task<List<CustomerReturnOrder>> OrdersAsync(int warehouseId,string? search,int skip,int take);
 Task<List<ReturnReceipt>> ReceiptsAsync(int warehouseId);
 Task<Dictionary<long,int>> ReceivedCountsAsync(IEnumerable<long> orderIds);
 Task<CustomerReturnOrder?> OrderAsync(long id,int warehouseId);
 Task<ReturnReceipt?> ReceiptAsync(long id,int warehouseId);
 Task<OutboundItem?> SoldSerialAsync(long salesOrderId,long outboundId,string serial);
 Task<InboundItem?> OriginalInboundItemAsync(long serialItemId);
 Task<bool> AlreadyReturnedAsync(long outboundItemId,long? excludeId=null);
 Task<int> ReceivedLineCountAsync(long returnLineId,long? excludeId=null);
 Task<T> TransactionAsync<T>(int actor,Func<Task<T>> work);
 Task SaveAsync();
 void AddReceipt(ReturnReceipt receipt);
 void AddItem(ReturnReceiptItem item);
 void TouchReceipt(ReturnReceipt receipt);
 Task PostAsync(ReturnReceipt receipt,int actor);
}
