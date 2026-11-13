using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.Repositories.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.Returns;
namespace PRN222_EWMS_Web.Services.WarehouseStaff;
public class ReturnReceiveService(IReturnReceiveRepository repo, ISourceReservationService commitments):IReturnReceiveService
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
  RequireOpen(r.ReturnOrder);
  var sold=await repo.SoldSerialAsync(r.SalesOrderId,r.OutboundId,serial)??throw new ArgumentException("Serial không thuộc hàng đã bán trong đơn này.");
  if(sold.OutboundId!=r.OutboundId)throw new ArgumentException("Serial thuộc phiếu xuất khác của đơn bán này.");
  if(await repo.AlreadyReturnedAsync(sold.OutboundItemId))throw new InvalidOperationException("Serial đã được nhận trả trước đó.");
  var line=r.ReturnOrder.CustomerReturnOrderLineReturnOrders.SingleOrDefault(l=>l.SolineId==sold.SolineId)??throw new ArgumentException("Sản phẩm không có trong đơn trả hàng.");
  if(await repo.ReceivedLineCountAsync(line.ReturnLineId)>=line.RequestedQuantity-line.CancelledQuantity)throw new InvalidOperationException("Dòng hàng này đã nhận đủ số lượng.");
  var original=await repo.OriginalInboundItemAsync(sold.SerialItemId);
  return new ReturnSerialPreview(sold.SerialItem.SerialNumber,sold.SerialItem.Product.ProductName,sold.SerialItem.ManufacturingMonth,sold.SerialItem.ManufacturingYear,sold.ItemCondition,original?.ConditionNote);
 }

    private static void RequireOpen(CustomerReturnOrder order)
    {
        if (order.Status is not ("OPEN" or "PARTIALLY_RECEIVED"))
            throw new InvalidOperationException("Chỉ nhận hàng từ đơn trả đã xác nhận và còn mở.");
    }

    private Task<T> ChangeAsync<T>(int userId, int warehouseId, Func<Task<T>> work) =>
        repo.TransactionAsync(userId, async () =>
        {
            await commitments.LockWarehouseAsync(warehouseId);
            if (await repo.WarehouseAsync(userId) != warehouseId)
                throw new InvalidOperationException("Phân công kho hoặc quyền thao tác vừa thay đổi. Hãy tải lại trang.");
            return await work();
        });

    public async Task<long> StartAsync(int userId, long returnOrderId, long outboundId)
    {
        var warehouseId = await Warehouse(userId);
        return await ChangeAsync(userId, warehouseId, async () =>
        {
            var order = await repo.OrderAsync(returnOrderId, warehouseId)
                ?? throw new ArgumentException("Không tìm thấy đơn trả hàng trong kho.");
            RequireOpen(order);
            if (!order.SalesOrder.Outbounds.Any(o => o.OutboundId == outboundId && o.Status == "DONE"))
                throw new ArgumentException("Phiếu xuất không thuộc đơn bán đã hoàn tất.");
            var draft = order.ReturnReceiptReturnOrders.SingleOrDefault(r => r.OutboundId == outboundId && r.Status == "DRAFT");
            if (draft is not null) return draft.ReturnReceiptId;
            var remaining = order.CustomerReturnOrderLineReturnOrders.Sum(l => (long)l.RequestedQuantity - l.CancelledQuantity)
                - order.ReturnReceiptReturnOrders.Where(r => r.Status is "DRAFT" or "DONE")
                    .Sum(r => (long)r.ReturnReceiptItemReturnReceipts.Count(i => i.IsActive));
            if (remaining <= 0) throw new InvalidOperationException("Đơn trả đã hết số lượng có thể nhận.");
            var receipt = new ReturnReceipt
            {
                ReturnOrderId = returnOrderId, OutboundId = outboundId, SalesOrderId = order.SalesOrderId,
                CreatedByUserId = userId, ReceiptCode = "RR-" + Guid.NewGuid().ToString("N"),
                ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), Status = "DRAFT", CreatedAt = DateTime.UtcNow
            };
            repo.AddReceipt(receipt);
            await repo.SaveAsync();
            return receipt.ReturnReceiptId;
        });
    }

    public async Task AddSerialAsync(int userId, long id, string serial, string condition, string? note, string version)
    {
        serial = serial?.Trim() ?? "";
        if (serial.Length is < 1 or > 100 || serial.Any(char.IsControl) || condition is not ("GOOD" or "DAMAGED")
            || condition == "DAMAGED" && string.IsNullOrWhiteSpace(note) || note?.Length > 1000)
            throw new ArgumentException("Serial hoặc tình trạng không hợp lệ; hàng hỏng cần ghi chú.");
        var warehouseId = await Warehouse(userId);
        await ChangeAsync(userId, warehouseId, async () =>
        {
            var receipt = await repo.ReceiptAsync(id, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            Version(receipt, version);
            if (receipt.Status != "DRAFT") throw new InvalidOperationException("Phiếu đã khóa.");
            RequireOpen(receipt.ReturnOrder);
            var sold = await repo.SoldSerialAsync(receipt.SalesOrderId, receipt.OutboundId, serial)
                ?? throw new ArgumentException("Serial phải đang SOLD và thuộc đúng phiếu xuất đã chọn.");
            if (await repo.AlreadyReturnedAsync(sold.OutboundItemId))
                throw new InvalidOperationException("Serial đã được nhận trả hoặc đang nằm trong phiếu nhận trả khác.");
            var line = receipt.ReturnOrder.CustomerReturnOrderLineReturnOrders.SingleOrDefault(l => l.SolineId == sold.SolineId)
                ?? throw new ArgumentException("Sản phẩm không thuộc đơn trả hàng.");
            if (line.ProductId != sold.ProductId || await repo.ReceivedLineCountAsync(line.ReturnLineId) >= line.RequestedQuantity - line.CancelledQuantity)
                throw new InvalidOperationException("Dòng hàng không khớp hoặc đã đủ số lượng nhận.");
            repo.AddItem(new ReturnReceiptItem
            {
                ReturnReceiptId = id, ReturnOrderId = receipt.ReturnOrderId, OutboundId = receipt.OutboundId,
                ReturnLineId = line.ReturnLineId, OutboundItemId = sold.OutboundItemId, SolineId = sold.SolineId,
                ReceivedCondition = condition, ConditionNote = note?.Trim(), ScannedAt = DateTime.UtcNow, IsActive = true
            });
            repo.TouchReceipt(receipt);
            await repo.SaveAsync();
            return true;
        });
    }

    public async Task RemoveSerialAsync(int userId, long id, long itemId, string version)
    {
        var warehouseId = await Warehouse(userId);
        await ChangeAsync(userId, warehouseId, async () =>
        {
            var receipt = await repo.ReceiptAsync(id, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            Version(receipt, version);
            if (receipt.Status != "DRAFT") throw new InvalidOperationException("Phiếu đã khóa.");
            var item = receipt.ReturnReceiptItemReturnReceipts.SingleOrDefault(i => i.ReturnItemId == itemId && i.IsActive)
                ?? throw new ArgumentException("Không tìm thấy serial.");
            item.IsActive = false;
            repo.TouchReceipt(receipt);
            await repo.SaveAsync();
            return true;
        });
    }

    public async Task CompleteAsync(int userId, long id, string version)
    {
        var warehouseId = await Warehouse(userId);
        await ChangeAsync(userId, warehouseId, async () =>
        {
            var receipt = await repo.ReceiptAsync(id, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            if (receipt.Status == "DONE") return true;
            Version(receipt, version);
            if (receipt.Status != "DRAFT") throw new InvalidOperationException("Phiếu đã khóa.");
            RequireOpen(receipt.ReturnOrder);
            var block = await commitments.GetReceivingBlockReasonAsync(warehouseId);
            if (block is not null) throw new InvalidOperationException(block);
            var items = receipt.ReturnReceiptItemReturnReceipts.Where(i => i.IsActive).ToList();
            if (items.Count == 0) throw new InvalidOperationException("Cần ít nhất một serial để hoàn tất.");
            foreach (var group in items.GroupBy(i => i.ReturnLineId))
            {
                var line = receipt.ReturnOrder.CustomerReturnOrderLineReturnOrders.SingleOrDefault(l => l.ReturnLineId == group.Key)
                    ?? throw new InvalidOperationException("Dòng hàng không còn thuộc đơn trả.");
                if (await repo.ReceivedLineCountAsync(line.ReturnLineId) > line.RequestedQuantity - line.CancelledQuantity)
                    throw new InvalidOperationException("Tổng số lượng các phiếu nhận vượt quá dòng đơn trả.");
                foreach (var item in group)
                {
                    var sold = await repo.SoldSerialAsync(receipt.SalesOrderId, receipt.OutboundId, item.OutboundItem.SerialItem.SerialNumber);
                    if (sold is null || sold.OutboundItemId != item.OutboundItemId || sold.SolineId != line.SolineId
                        || sold.ProductId != line.ProductId || item.ReturnOrderId != receipt.ReturnOrderId || item.OutboundId != receipt.OutboundId
                        || await repo.AlreadyReturnedAsync(item.OutboundItemId, item.ReturnItemId))
                        throw new InvalidOperationException("Serial không còn SOLD, sai lần xuất hoặc đã được nhận trả ở phiếu khác.");
                    if (item.ReceivedCondition is not ("GOOD" or "DAMAGED")
                        || item.ReceivedCondition == "DAMAGED" && string.IsNullOrWhiteSpace(item.ConditionNote))
                        throw new InvalidOperationException("Serial thiếu tình trạng thực nhận hoặc ghi chú hàng hỏng.");
                }
            }
            await repo.PostAsync(receipt, userId);
            await commitments.ReconcileWarehouseAsync(warehouseId, "Đã hoàn tất nhận hàng khách trả.");
            return true;
        });
    }

    public async Task CancelAsync(int userId, long id, string version)
    {
        var warehouseId = await Warehouse(userId);
        await ChangeAsync(userId, warehouseId, async () =>
        {
            var receipt = await repo.ReceiptAsync(id, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            if (receipt.Status == "CANCELLED") return true;
            Version(receipt, version);
            if (receipt.Status != "DRAFT") throw new InvalidOperationException("Chỉ hủy được phiếu nháp.");
            foreach (var item in receipt.ReturnReceiptItemReturnReceipts.Where(i => i.IsActive)) item.IsActive = false;
            repo.TouchReceipt(receipt);
            await repo.SaveAsync();
            receipt.Status = "CANCELLED";
            await repo.SaveAsync();
            return true;
        });
    }
}
