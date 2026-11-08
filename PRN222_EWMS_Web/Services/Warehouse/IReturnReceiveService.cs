using PRN222_EWMS_Web.ViewModels.Returns;
namespace PRN222_EWMS_Web.Services.WarehouseStaff;
public interface IReturnReceiveService
{
 Task<ReturnReceivePage> ListAsync(int userId,string? search,int page);
 Task<ReturnReceiveDetail?> DetailAsync(int userId,long id);
 Task<long> StartAsync(int userId,long returnOrderId,long outboundId);
 Task AddSerialAsync(int userId,long receiptId,string serial,string condition,string? note,string version);
 Task RemoveSerialAsync(int userId,long receiptId,long itemId,string version);
 Task CompleteAsync(int userId,long receiptId,string version);
 Task CancelAsync(int userId,long receiptId,string version);
}
