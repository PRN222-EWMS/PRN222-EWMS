using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Purchasing;
using PRN222_EWMS_Web.Repositories.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.Returns;
namespace PRN222_EWMS_Web.Services.WarehouseStaff;
public class ReturnReceiveService(IReturnReceiveRepository repo, IWarehouseCapacityEngine capacity):IReturnReceiveService
{
 private async Task<int> Warehouse(int user)=>await repo.WarehouseAsync(user)??throw new UnauthorizedAccessException("Bạn chưa được phân công kho.");
 private static void Version(ReturnReceipt r,string v){if(Convert.ToBase64String(r.RowVersion)!=v)throw new InvalidOperationException("Phiếu đã thay đổi. Tải lại trang.");}
 public async Task<ReturnReceivePage> ListAsync(int userId,string? search,int page)
 {
  var w=await Warehouse(userId);search=string.IsNullOrWhiteSpace(search)?null:search.Trim();if(search?.Length>100)throw new ArgumentException("Mã tìm kiếm quá dài.");
  var count=await repo.CountOrdersAsync(w,search);var pages=Math.Max(1,(int)Math.Ceiling(count/10d));page=Math.Clamp(page,1,pages);
  var orders=await repo.OrdersAsync(w,search,(page-1)*10,10);var receipts=await repo.ReceiptsAsync(w);var received=await repo.ReceivedCountsAsync(orders.Select(o=>o.ReturnOrderId));
  return new ReturnReceivePage{Page=page,TotalPages=pages,Search=search,Orders=orders.Select(o=>new ReturnOrderRow(o.ReturnOrderId,o.ReturnCode,o.SalesOrder.Socode,o.SalesOrder.Customer.CustomerName,o.Status,o.CustomerReturnOrderLineReturnOrders.Sum(l=>l.RequestedQuantity-l.CancelledQuantity),received.GetValueOrDefault(o.ReturnOrderId),o.SalesOrder.Outbounds.Where(x=>x.Status=="DONE").Select(x=>new ReturnOutboundOption(x.OutboundId,x.IssueCode)).ToList())).ToList(),Receipts=receipts.Select(r=>new ReturnReceiptRow(r.ReturnReceiptId,r.ReceiptCode,r.ReturnOrder.ReturnCode,r.Status,r.ReturnReceiptItemReturnReceipts.Count(i=>i.IsActive),r.CreatedAt)).ToList()};
 }
 public async Task<ReturnReceiveDetail?> DetailAsync(int userId,long id)
 {
  var r=await repo.ReceiptAsync(id,await Warehouse(userId));if(r is null)return null;
  var rows=new List<ReturnSerialRow>();
  foreach(var item in r.ReturnReceiptItemReturnReceipts.Where(i=>i.IsActive).OrderByDescending(i=>i.ScannedAt))
  {
   var sold=item.OutboundItem;var original=await repo.OriginalInboundItemAsync(sold.SerialItemId);
   rows.Add(new ReturnSerialRow(item.ReturnItemId,sold.SerialItem.SerialNumber,sold.SerialItem.Product.ProductName,item.ReceivedCondition??"—",item.ConditionNote,item.IsActive,sold.SerialItem.ManufacturingMonth,sold.SerialItem.ManufacturingYear,sold.ItemCondition,original?.ConditionNote));
  }
  return new ReturnReceiveDetail{Id=id,ReturnOrderId=r.ReturnOrderId,Code=r.ReceiptCode,ReturnCode=r.ReturnOrder.ReturnCode,SalesCode=r.ReturnOrder.SalesOrder.Socode,Customer=r.ReturnOrder.SalesOrder.Customer.CustomerName,Status=r.Status,Version=Convert.ToBase64String(r.RowVersion),Requested=r.ReturnOrder.CustomerReturnOrderLineReturnOrders.Sum(l=>l.RequestedQuantity-l.CancelledQuantity),Received=rows.Count,Note=r.Note,Serials=rows};
 }
 public async Task<ReturnSerialPreview> PreviewSerialAsync(int userId,long receiptId,string serial)
 {
  serial=serial?.Trim()??"";if(serial.Length is <1 or >100)throw new ArgumentException("Hãy nhập Serial hợp lệ.");
  var r=await repo.ReceiptAsync(receiptId,await Warehouse(userId))??throw new ArgumentException("Không tìm thấy phiếu nhận hàng trả trong kho của bạn.");
  if(r.Status!="DRAFT")throw new InvalidOperationException("Phiếu đã khóa, không thể quét thêm Serial.");
  var sold=await repo.SoldSerialAsync(r.SalesOrderId,serial)??throw new ArgumentException("Serial không thuộc hàng đã bán trong đơn này.");
  if(sold.OutboundId!=r.OutboundId)throw new ArgumentException("Serial thuộc phiếu xuất khác của đơn bán này.");
  if(await repo.AlreadyReturnedAsync(sold.OutboundItemId))throw new InvalidOperationException("Serial đã được nhận trả trước đó.");
  var line=r.ReturnOrder.CustomerReturnOrderLineReturnOrders.SingleOrDefault(l=>l.SolineId==sold.SolineId)??throw new ArgumentException("Sản phẩm không có trong đơn trả hàng.");
  if(await repo.ReceivedLineCountAsync(line.ReturnLineId)>=line.RequestedQuantity-line.CancelledQuantity)throw new InvalidOperationException("Dòng hàng này đã nhận đủ số lượng.");
  var original=await repo.OriginalInboundItemAsync(sold.SerialItemId);
  return new ReturnSerialPreview(sold.SerialItem.SerialNumber,sold.SerialItem.Product.ProductName,sold.SerialItem.ManufacturingMonth,sold.SerialItem.ManufacturingYear,sold.ItemCondition,original?.ConditionNote);
 }
 public async Task<long> StartAsync(int userId,long returnOrderId,long outboundId)
 {
  var w=await Warehouse(userId);return await repo.TransactionAsync(userId,async()=>{var o=await repo.OrderAsync(returnOrderId,w)??throw new ArgumentException("Không tìm thấy đơn trả hàng trong kho.");if(o.Status is "CANCELLED" or "CLOSED")throw new InvalidOperationException("Đơn trả hàng đã đóng hoặc bị hủy.");if(!o.SalesOrder.Outbounds.Any(x=>x.OutboundId==outboundId&&x.Status=="DONE"))throw new ArgumentException("Phiếu xuất không thuộc đơn bán đã hoàn tất.");var draft=o.ReturnReceiptReturnOrders.FirstOrDefault(r=>r.OutboundId==outboundId&&r.Status=="DRAFT");if(draft is not null)return draft.ReturnReceiptId;var r=new ReturnReceipt{ReturnOrderId=returnOrderId,OutboundId=outboundId,SalesOrderId=o.SalesOrderId,CreatedByUserId=userId,ReceiptCode="RR-"+Guid.NewGuid().ToString("N"),ReceiptDate=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),Status="DRAFT",CreatedAt=DateTime.UtcNow};repo.AddReceipt(r);await repo.SaveAsync();return r.ReturnReceiptId;});
 }
 public async Task AddSerialAsync(int userId,long id,string serial,string condition,string? note,string version)
 {
  serial=serial?.Trim()??"";if(serial.Length is <1 or >100||condition is not("GOOD" or "DAMAGED")||condition=="DAMAGED"&&string.IsNullOrWhiteSpace(note)||note?.Length>1000)throw new ArgumentException("Serial hoặc tình trạng không hợp lệ; hàng hỏng cần ghi chú.");var w=await Warehouse(userId);
  await repo.TransactionAsync(userId,async()=>{var r=await repo.ReceiptAsync(id,w)??throw new ArgumentException("Không tìm thấy phiếu.");Version(r,version);if(r.Status!="DRAFT")throw new InvalidOperationException("Phiếu đã khóa.");var sold=await repo.SoldSerialAsync(r.SalesOrderId,serial)??throw new ArgumentException("Serial này không thuộc hàng đã xuất cho đơn bán.");if(sold.OutboundId!=r.OutboundId)throw new ArgumentException("Serial thuộc phiếu xuất khác. Chọn đúng phiếu xuất để nhận.");if(await repo.AlreadyReturnedAsync(sold.OutboundItemId))throw new InvalidOperationException("Serial đã được nhận trả trước đó.");var line=r.ReturnOrder.CustomerReturnOrderLineReturnOrders.SingleOrDefault(l=>l.SolineId==sold.SolineId)??throw new ArgumentException("Sản phẩm không thuộc đơn trả hàng.");if(await repo.ReceivedLineCountAsync(line.ReturnLineId)>=line.RequestedQuantity-line.CancelledQuantity)throw new InvalidOperationException("Dòng hàng trả đã đủ số lượng.");repo.AddItem(new ReturnReceiptItem{ReturnReceiptId=id,ReturnOrderId=r.ReturnOrderId,OutboundId=r.OutboundId,ReturnLineId=line.ReturnLineId,OutboundItemId=sold.OutboundItemId,SolineId=sold.SolineId,ReceivedCondition=condition,ConditionNote=note?.Trim(),ScannedAt=DateTime.UtcNow,IsActive=true});await repo.SaveAsync();return true;});
 }
 public async Task RemoveSerialAsync(int userId,long id,long itemId,string version){var w=await Warehouse(userId);await repo.TransactionAsync(userId,async()=>{var r=await repo.ReceiptAsync(id,w)??throw new ArgumentException("Không tìm thấy phiếu.");Version(r,version);if(r.Status!="DRAFT")throw new InvalidOperationException("Phiếu đã khóa.");var item=r.ReturnReceiptItemReturnReceipts.SingleOrDefault(i=>i.ReturnItemId==itemId&&i.IsActive)??throw new ArgumentException("Không tìm thấy Serial.");item.IsActive=false;await repo.SaveAsync();return true;});}
 public async Task CompleteAsync(int userId,long id,string version){var w=await Warehouse(userId);await repo.TransactionAsync(userId,async()=>{await capacity.LockWarehouseAsync(w);var r=await repo.ReceiptAsync(id,w)??throw new ArgumentException("Không tìm thấy phiếu.");Version(r,version);if(r.Status!="DRAFT")throw new InvalidOperationException("Phiếu đã khóa.");if(!r.ReturnReceiptItemReturnReceipts.Any(i=>i.IsActive))throw new InvalidOperationException("Cần ít nhất một Serial để hoàn tất.");await repo.PostAsync(r,userId);return true;});}
 public async Task CancelAsync(int userId,long id,string version){var w=await Warehouse(userId);await repo.TransactionAsync(userId,async()=>{var r=await repo.ReceiptAsync(id,w)??throw new ArgumentException("Không tìm thấy phiếu.");Version(r,version);if(r.Status!="DRAFT")throw new InvalidOperationException("Chỉ hủy được phiếu nháp.");foreach(var i in r.ReturnReceiptItemReturnReceipts)i.IsActive=false;await repo.SaveAsync();r.Status="CANCELLED";await repo.SaveAsync();return true;});}
}
