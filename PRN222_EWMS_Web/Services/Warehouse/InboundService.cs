using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.Inbound;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public class InboundService(IInboundRepository repo, PRN222_EWMS_Web.Services.Warehouse.IOutboundSupplyService supply) : IInboundService
{
    private const int PageSize = 10;
    private static readonly TimeZoneInfo VietnamTime = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    private async Task<int> WarehouseAsync(int userId) => await repo.GetWarehouseIdAsync(userId) ?? 0;

    public async Task<InboundListViewModel> ListAsync(int userId, string? search, string? status, DateOnly? from, DateOnly? to, int page)
    {
        var warehouseId = await WarehouseAsync(userId);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        status = status is "DRAFT" or "DONE" or "CANCELLED" ? status : null;
        var count = await repo.CountAsync(warehouseId, search, status, from, to);
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var items = await repo.ListAsync(warehouseId, search, status, from, to, (page - 1) * PageSize, PageSize);
        return new InboundListViewModel
        {
            Search = search, Status = status, FromDate = from, ToDate = to, Page = page, TotalItems = count, TotalPages = pages,
            Items = items.Select(i => new InboundSummary(i.InboundId, i.ReceiptCode, i.PurchaseOrder.Pocode,
                i.PurchaseOrder.Supplier.SupplierName, i.PurchaseOrder.Warehouse.WarehouseName, i.Status,
                i.InboundItemInbounds.Count(x => x.IsActive), i.PurchaseOrder.PurchaseOrderLines.Sum(x => x.OrderedQuantity),
                i.CreatedByUser.FullName, i.CreatedAt, i.CompletedAt)).ToList()
        };
    }

    public async Task<InboundCreateViewModel> GetCreateAsync(int userId, long? orderId = null)
    {
        var warehouseId = await WarehouseAsync(userId);
        var orders = await repo.GetEligibleOrdersAsync(warehouseId);
        var options = new List<InboundOrderOption>();
        foreach (var order in orders)
        {
            var items = await repo.GetOrderItemsAsync(order.PurchaseOrderId);
            var remaining = order.PurchaseOrderLines.Sum(l => l.OrderedQuantity - l.ClosedQuantity)
                - items.Count(x => x.Inbound.Status is "DRAFT" or "DONE");
            if (remaining > 0) options.Add(new(order.PurchaseOrderId, order.Pocode,
                order.Supplier.SupplierName, order.Warehouse.WarehouseName, remaining));
        }
        return new InboundCreateViewModel { PurchaseOrderId = orderId ?? 0, Orders = options };
    }

    public async Task<InboundDetailViewModel?> GetDetailAsync(int userId, long id)
    {
        var warehouseId = await WarehouseAsync(userId);
        var inbound = await repo.GetInboundAsync(id, warehouseId);
        if (inbound is null) return null;
        var allItems = await repo.GetOrderItemsAsync(inbound.PurchaseOrderId);
        var userNames = await repo.GetUserNamesAsync(inbound.InboundItemInbounds.Where(x => x.ScannedByUserId.HasValue).Select(x => x.ScannedByUserId!.Value).Distinct());
        var model = new InboundDetailViewModel
        {
            Id = id, Code = inbound.ReceiptCode, Status = inbound.Status,
            PoCode = inbound.PurchaseOrder.Pocode, Supplier = inbound.PurchaseOrder.Supplier.SupplierName,
            Warehouse = inbound.PurchaseOrder.Warehouse.WarehouseName,
            CreatedBy = inbound.CreatedByUser.FullName, CreatedAt = inbound.CreatedAt,
            CompletedAt = inbound.CompletedAt, PerformedBy = inbound.PerformedByUser?.FullName,
            Note = inbound.Note, CancelReason = inbound.CancelReason,
            Version = Convert.ToBase64String(inbound.RowVersion),
            Lines = inbound.PurchaseOrder.PurchaseOrderLines.Select(line =>
            {
                var received = allItems.Count(x => x.PolineId == line.PolineId && x.Inbound.Status == "DONE");
                var thisDraft = allItems.Count(x => x.PolineId == line.PolineId && x.InboundId == id && x.Inbound.Status == "DRAFT");
                var otherDraft = allItems.Count(x => x.PolineId == line.PolineId && x.InboundId != id && x.Inbound.Status == "DRAFT");
                return new InboundLineProgress(line.PolineId, line.Product.ProductCode, line.Product.ProductName,
                    line.OrderedQuantity, line.ClosedQuantity, received, thisDraft, otherDraft,
                    Math.Max(0, line.OrderedQuantity - line.ClosedQuantity - received - thisDraft - otherDraft));
            }).ToList(),
            Serials = inbound.InboundItemInbounds.OrderByDescending(x => x.ScannedAt).Select(x => new InboundSerialRow(
                x.InboundItemId, x.PolineId, x.ScannedSerial, x.Poline.Product.ProductName,
                x.ManufacturingMonth, x.ManufacturingYear, x.ReceivedCondition ?? "—", x.ConditionNote,
                x.CaptureMethod, x.ScannedByUserId is int scannedBy && userNames.TryGetValue(scannedBy, out var name) ? name : null,
                x.ScannedAt, x.IsActive)).ToList()
        };
        model.NewSerial.Version = model.Version;
        return model;
    }

    public async Task<long> CreateDraftAsync(int userId, InboundCreateViewModel input)
    {
        var warehouseId = await WarehouseAsync(userId);
        if (input.Note?.Length > 1000 || !Guid.TryParseExact(input.RequestToken, "N", out _))
            throw new ArgumentException("Dữ liệu phiếu không hợp lệ.");
        var code = "IB-" + input.RequestToken.ToLowerInvariant();
        return await repo.InTransactionAsync(userId, async () =>
        {
            var retry = await repo.GetByCodeAsync(code, warehouseId);
            if (retry is not null) return retry.InboundId;
            var order = await repo.GetOrderAsync(input.PurchaseOrderId, warehouseId)
                ?? throw new ArgumentException("Đơn mua không thuộc kho của bạn.");
            if (order.Status is not ("CONFIRMED" or "PARTIALLY_RECEIVED"))
                throw new InvalidOperationException("Đơn mua chưa sẵn sàng nhận.");
            var draft = await repo.GetDraftForOrderAsync(order.PurchaseOrderId);
            if (draft is not null) return draft.InboundId;
            var items = await repo.GetOrderItemsAsync(order.PurchaseOrderId);
            var remaining = order.PurchaseOrderLines.Sum(l => l.OrderedQuantity - l.ClosedQuantity)
                - items.Count(x => x.Inbound.Status is "DRAFT" or "DONE");
            if (remaining <= 0) throw new InvalidOperationException("Đơn mua đã hết số lượng có thể nhận.");
            var inbound = new Inbound
            {
                PurchaseOrderId = order.PurchaseOrderId, CreatedByUserId = userId,
                ReceiptCode = code, ReceiptDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime)),
                Status = "DRAFT", Note = input.Note?.Trim()
            };
            repo.AddInbound(inbound);
            await repo.SaveAsync();
            return inbound.InboundId;
        });
    }

    private static void CheckVersion(Inbound inbound, string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version != Convert.ToBase64String(inbound.RowVersion))
            throw new DbUpdateConcurrencyException("Phiếu đã được thay đổi. Vui lòng tải lại trang.");
    }

    private static void ValidateSerial(ReceiveSerialInput input)
    {
        input.Serial = input.Serial?.Trim() ?? "";
        if (input.Serial.Length is < 1 or > 100 || input.Serial.Any(char.IsControl))
            throw new ArgumentException("Serial phải dài 1–100 ký tự và không chứa ký tự điều khiển.");
        if (input.Condition is not ("GOOD" or "DAMAGED")) throw new ArgumentException("Tình trạng không hợp lệ.");
        if (input.CaptureMethod is not ("MANUAL" or "CAMERA" or "IMAGE_UPLOAD")) throw new ArgumentException("Phương thức nhận diện không hợp lệ.");
        if (input.ConditionNote?.Length > 1000 || input.Condition == "DAMAGED" && string.IsNullOrWhiteSpace(input.ConditionNote))
            throw new ArgumentException("Hàng hỏng cần ghi chú tình trạng tối đa 1000 ký tự.");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTime);
        if (input.Year < 1900 || input.Month is < 1 or > 12 || input.Year > now.Year || input.Year == now.Year && input.Month > now.Month)
            throw new ArgumentException("Tháng/năm sản xuất không hợp lệ hoặc ở tương lai.");
    }

    public Task AddItemAsync(int userId, long inboundId, ReceiveSerialInput input) => ChangeItemAsync(userId, inboundId, null, input);
    public Task UpdateItemAsync(int userId, long inboundId, long itemId, ReceiveSerialInput input) => ChangeItemAsync(userId, inboundId, itemId, input);

    private async Task ChangeItemAsync(int userId, long inboundId, long? itemId, ReceiveSerialInput input)
    {
        ValidateSerial(input);
        var warehouseId = await WarehouseAsync(userId);
        await repo.InTransactionAsync(userId, async () =>
        {
            var inbound = await repo.GetInboundAsync(inboundId, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            CheckVersion(inbound, input.Version);
            if (inbound.Status != "DRAFT") throw new InvalidOperationException("Phiếu đã khóa.");
            var line = inbound.PurchaseOrder.PurchaseOrderLines.SingleOrDefault(x => x.PolineId == input.LineId)
                ?? throw new ArgumentException("Dòng sản phẩm không thuộc đơn mua.");
            var item = itemId.HasValue ? await repo.GetItemAsync(itemId.Value, inboundId) : null;
            if (itemId.HasValue && (item is null || !item.IsActive)) throw new ArgumentException("Serial không còn hiệu lực.");
            if (await repo.SerialExistsAsync(input.Serial, itemId)) throw new InvalidOperationException("Serial đã tồn tại.");
            var all = await repo.GetOrderItemsAsync(inbound.PurchaseOrderId);
            var used = all.Count(x => x.PolineId == line.PolineId && (x.Inbound.Status is "DRAFT" or "DONE") && x.InboundItemId != itemId);
            if (used >= line.OrderedQuantity - line.ClosedQuantity) throw new InvalidOperationException("Dòng PO đã hết số lượng có thể nhận.");
            if (item is null)
            {
                item = new InboundItem { InboundId = inboundId, PurchaseOrderId = inbound.PurchaseOrderId,
                    PolineId = line.PolineId, ProductId = line.ProductId, IsActive = true };
                repo.AddItem(item);
            }
            else { item.PolineId = line.PolineId; item.ProductId = line.ProductId; }
            item.ScannedSerial = input.Serial;
            item.ManufacturingMonth = (byte)input.Month;
            item.ManufacturingYear = (short)input.Year;
            item.ReceivedCondition = input.Condition;
            item.ConditionNote = input.ConditionNote?.Trim();
            item.CaptureMethod = input.CaptureMethod;
            item.ScannedByUserId = userId;
            item.ScannedAt = DateTime.UtcNow;
            repo.TouchInbound(inbound);
            await repo.SaveAsync();
            return true;
        });
    }

    public async Task RemoveItemAsync(int userId, long inboundId, long itemId, string version)
    {
        var warehouseId = await WarehouseAsync(userId);
        await repo.InTransactionAsync(userId, async () =>
        {
            var inbound = await repo.GetInboundAsync(inboundId, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            CheckVersion(inbound, version);
            if (inbound.Status != "DRAFT") throw new InvalidOperationException("Phiếu đã khóa.");
            var item = await repo.GetItemAsync(itemId, inboundId) ?? throw new ArgumentException("Không tìm thấy Serial.");
            item.IsActive = false;
            repo.TouchInbound(inbound);
            await repo.SaveAsync();
            return true;
        });
    }

    public async Task CompleteAsync(int userId, long inboundId, string version)
    {
        var warehouseId = await WarehouseAsync(userId);
        await repo.InTransactionAsync(userId, async () =>
        {
            await supply.LockWarehouseAsync(warehouseId);
            var inbound = await repo.GetInboundAsync(inboundId, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            if (inbound.Status == "DONE") return true;
            CheckVersion(inbound, version);
            if (inbound.Status != "DRAFT") throw new InvalidOperationException("Phiếu đã khóa.");
            var items = inbound.InboundItemInbounds.Where(x => x.IsActive).ToList();
            var receivingBlock = await supply.GetReceivingBlockReasonAsync(warehouseId);
            if (receivingBlock is not null) throw new InvalidOperationException(receivingBlock);
            if (inbound.PurchaseOrder.Status is not ("CONFIRMED" or "PARTIALLY_RECEIVED"))
                throw new InvalidOperationException("Đơn mua không còn mở để nhận hàng.");
            if (items.Count == 0) throw new InvalidOperationException("Cần ít nhất một Serial để hoàn tất.");
            var all = await repo.GetOrderItemsAsync(inbound.PurchaseOrderId);
            foreach (var line in inbound.PurchaseOrder.PurchaseOrderLines)
                if (all.Count(x => x.PolineId == line.PolineId && (x.Inbound.Status is "DRAFT" or "DONE")) > line.OrderedQuantity - line.ClosedQuantity)
                    throw new InvalidOperationException("Số lượng nhận vượt dòng PO.");
            var now = DateTime.UtcNow;
            foreach (var line in inbound.PurchaseOrder.PurchaseOrderLines)
            {
                var newlyReceived = items.Count(x => x.PolineId == line.PolineId);
                if (newlyReceived == 0) continue;
                var incoming = await repo.GetIncomingSourceAsync(line.PolineId);
                if (incoming is not null)
                {
                    if (incoming.RemainingQuantity < newlyReceived || incoming.EligibleQuantity < newlyReceived)
                        throw new InvalidOperationException("Nguồn hàng dự kiến không đủ để ghi nhận phiếu.");
                    incoming.RemainingQuantity -= newlyReceived;
                    incoming.EligibleQuantity -= newlyReceived;
                }
            }
            foreach (var group in items.GroupBy(x => new { x.ProductId, Condition = x.ReceivedCondition! }))
            {
                var source = new SupplySource { ProductId = group.Key.ProductId, WarehouseId = warehouseId,
                    ItemCondition = group.Key.Condition, SourceType = "INBOUND", SupplyState = "PHYSICAL",
                    InboundId = inboundId, EligibleQuantity = group.Count(),
                    RemainingQuantity = group.Count(),
                    AvailableFrom = DateOnly.FromDateTime(now), CreatedAt = now };
                repo.AddSource(source);
                await repo.SaveAsync();
                foreach (var item in group)
                {
                    var serial = new SerialItem { ProductId = item.ProductId, SerialNumber = item.ScannedSerial,
                        ManufacturingMonth = item.ManufacturingMonth, ManufacturingYear = item.ManufacturingYear,
                        OriginType = "PURCHASE", OriginalInboundId = inboundId, CurrentWarehouseId = warehouseId,
                        CurrentLocationId = null, CurrentSupplySourceId = source.SupplySourceId,
                        CurrentSupplyState = "PHYSICAL", StockState = "IN_WAREHOUSE",
                        ItemCondition = group.Key.Condition, IsQuarantined = false,
                        QuarantineReason = null, RegisteredAt = now };
                    repo.AddSerial(serial);
                    await repo.SaveAsync();
                    item.SerialItemId = serial.SerialItemId;
                    repo.AddMovement(new SerialMovement { SerialItemId = serial.SerialItemId, PerformedByUserId = userId,
                        EventType = "PURCHASE_RECEIPT", OccurredAt = now, ToState = "IN_WAREHOUSE", ToCondition = group.Key.Condition,
                        ToWarehouseId = warehouseId, ToQuarantined = serial.IsQuarantined, InboundId = inboundId });
                }
            }
            // The posting trigger validates linked serials when the receipt becomes DONE.
            // Flush item links and movements first, while the receipt is still DRAFT.
            await repo.SaveAsync();
            if (items.Any(item => item.SerialItemId is null || item.ReceivedCondition is null))
                throw new InvalidOperationException("Chưa ghi nhận đủ Serial tồn kho hoặc tình trạng hàng cho phiếu nhập.");
            inbound.Status = "DONE";
            inbound.PerformedByUserId = userId;
            inbound.CompletedAt = now;
            var receivedTotal = all.Count(x => x.Inbound.Status == "DONE") + items.Count;
            var expectedTotal = inbound.PurchaseOrder.PurchaseOrderLines.Sum(x => x.OrderedQuantity - x.ClosedQuantity);
            inbound.PurchaseOrder.Status = receivedTotal >= expectedTotal ? "RECEIVED" : "PARTIALLY_RECEIVED";
            await repo.SaveAsync();
            await supply.AfterInboundPostedAsync(inboundId);
            return true;
        });
    }

    public async Task CancelAsync(int userId, long inboundId, string version, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 900) throw new ArgumentException("Cần lý do hủy tối đa 900 ký tự.");
        var warehouseId = await WarehouseAsync(userId);
        await repo.InTransactionAsync(userId, async () =>
        {
            var inbound = await repo.GetInboundAsync(inboundId, warehouseId) ?? throw new ArgumentException("Không tìm thấy phiếu.");
            if (inbound.Status == "CANCELLED") return true;
            CheckVersion(inbound, version);
            if (inbound.Status != "DRAFT") throw new InvalidOperationException("Chỉ hủy được phiếu nháp.");
            foreach (var item in inbound.InboundItemInbounds) item.IsActive = false;
            await repo.SaveAsync();
            inbound.CancelReason = reason.Trim();
            inbound.Status = "CANCELLED";
            inbound.PerformedByUserId = userId;
            await repo.SaveAsync();
            return true;
        });
    }

    public async Task<InboundPendingPutawayViewModel> GetPendingPutawayAsync(int userId)
    {
        var warehouseId = await WarehouseAsync(userId);
        var items = await repo.GetPendingPutawayAsync(warehouseId);
        return new InboundPendingPutawayViewModel { Items = items.Select(item => new PendingPutawayRow(
            item.OriginalInboundId!.Value, item.OriginalInbound!.ReceiptCode, item.SerialNumber,
            item.Product.ProductCode, item.Product.ProductName, item.ItemCondition,
            item.CurrentWarehouse!.WarehouseName, item.RegisteredAt)).ToList() };
    }
}
