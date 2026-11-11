using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public class TransferReceiptWorkService(EwmsDbContext db, TimeProvider clock)
{
    private Task<TransferOrder?> LockDestinationOrderAsync(long orderId, int userId) =>
        db.TransferOrders
            .FromSqlInterpolated($"SELECT * FROM dbo.TransferOrder WITH (UPDLOCK, HOLDLOCK) WHERE TransferOrderID = {orderId}")
            .Where(o => db.Users.Any(u => u.UserId == userId && u.Status == "ACTIVE"
                && u.Role.RoleName == "WAREHOUSE_STAFF" && u.WarehouseId == o.ToWarehouseId))
            .SingleOrDefaultAsync();

    public async Task<(bool Success, string Message)> ClaimAsync(long orderId, int userId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await LockDestinationOrderAsync(orderId, userId);

        if (order is null)
            return (false, "Bạn chỉ có thể nhận hàng tại kho đích của mình.");
        if (order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Trạng thái hiện tại của lệnh chưa cho phép nhận hàng.");

        var hasIncoming = await db.VwTransferInTransits.AnyAsync(i =>
            i.TransferOrderId == orderId && i.ToWarehouseId == order.ToWarehouseId);
        if (!hasIncoming)
            return (false, "Lệnh chưa có serial đang chuyển đến kho.");

        var drafts = await db.TransferReceipts
            .Where(r => r.TransferOrderId == orderId && r.Status == "DRAFT")
            .OrderBy(r => r.TransferReceiptId).Take(2).ToListAsync();
        if (drafts.Count > 1)
            return (false, "Lệnh có nhiều phiếu nhận đang mở, chưa thể tự nhận đợt này.");

        var receipt = drafts.SingleOrDefault();
        if (receipt?.PerformedByUserId == userId)
            return (true, $"Bạn đang phụ trách phiếu nhận {receipt.ReceiptCode}.");
        if (receipt?.PerformedByUserId is not null)
            return (false, "Đợt nhận này đã có nhân viên khác phụ trách.");

        if (receipt is null)
        {
            receipt = new TransferReceipt
            {
                TransferOrderId = orderId,
                ReceiptCode = $"TRR-{Guid.NewGuid():N}",
                ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedByUserId = userId,
                Status = "DRAFT"
            };
            db.TransferReceipts.Add(receipt);
        }

        receipt.PerformedByUserId = userId;
        await SaveAsActorAsync(userId);
        await transaction.CommitAsync();
        return (true, $"Bạn đã nhận việc trên phiếu {receipt.ReceiptCode}.");
    }

    public async Task<(bool Success, string Message)> ScanAsync(long orderId, long receiptId,
        string? serialNumber, string? receivedCondition, string? conditionNote, int userId)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return (false, "Vui lòng nhập serial thực nhận.");
        var number = serialNumber.Trim();
        var condition = receivedCondition?.Trim().ToUpperInvariant();
        if (condition is not ("GOOD" or "DAMAGED"))
            return (false, "Vui lòng chọn tình trạng thực nhận.");
        var note = string.IsNullOrWhiteSpace(conditionNote) ? null : conditionNote.Trim();
        if (note is { Length: > 1000 })
            return (false, "Ghi chú tình trạng tối đa 1.000 ký tự.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await LockDestinationOrderAsync(orderId, userId);
        if (order is null || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Lệnh chưa sẵn sàng nhận hàng tại kho của bạn.");
        var canScan = await db.TransferReceipts.AnyAsync(r => r.TransferReceiptId == receiptId
            && r.TransferOrderId == orderId && r.Status == "DRAFT" && r.PerformedByUserId == userId);
        if (!canScan)
            return (false, "Bạn chỉ có thể quét vào phiếu nhận nháp mình phụ trách.");

        var incoming = await db.VwTransferInTransits.SingleOrDefaultAsync(i =>
            i.TransferOrderId == orderId && i.ToWarehouseId == order.ToWarehouseId && i.SerialNumber == number);
        if (incoming is null)
            return (false, "Serial không thuộc lô hàng đang chuyển đến theo lệnh này.");

        if (await db.TransferReceiptItems.AnyAsync(i => i.DispatchItemId == incoming.DispatchItemId && i.IsActive))
            return (false, "Serial đã được quét vào phiếu nhận.");

        db.TransferReceiptItems.Add(new TransferReceiptItem
        {
            TransferOrderId = orderId,
            TransferReceiptId = receiptId,
            DispatchItemId = incoming.DispatchItemId,
            ReceivedCondition = condition,
            ConditionNote = note,
            ScannedAt = DateTime.UtcNow,
            IsActive = true
        });
        try
        {
            await SaveAsActorAsync(userId);
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return (false, "Serial đã được quét vào phiếu nhận.");
        }
        return (true, $"Đã thêm serial {incoming.SerialNumber} vào phiếu nhận nháp.");
    }

    public async Task<(bool Success, string Message)> CompleteAsync(long orderId, long receiptId, int userId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await LockDestinationOrderAsync(orderId, userId);
        if (order is null)
            return (false, "Lệnh không thuộc kho đích của bạn.");

        var receipt = await db.TransferReceipts.SingleOrDefaultAsync(r => r.TransferReceiptId == receiptId
            && r.TransferOrderId == orderId && r.PerformedByUserId == userId);
        if (receipt is null)
            return (false, "Bạn chỉ có thể hoàn tất phiếu nhận mình phụ trách.");
        if (receipt.Status == "DONE")
            return (true, $"Phiếu {receipt.ReceiptCode} đã hoàn tất.");
        if (receipt.Status != "DRAFT" || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Trạng thái hiện tại chưa cho phép hoàn tất nhận.");

        var items = await db.TransferReceiptItems
            .Include(i => i.DispatchItem).ThenInclude(d => d.SerialItem)
            .Where(i => i.TransferReceiptId == receiptId && i.IsActive)
            .ToListAsync();
        if (items.Count == 0)
            return (false, "Hãy quét ít nhất một serial trước khi hoàn tất nhận.");

        var incomingIds = (await db.VwTransferInTransits
            .Where(i => i.TransferOrderId == orderId && i.ToWarehouseId == order.ToWarehouseId)
            .Select(i => i.DispatchItemId).ToListAsync()).ToHashSet();
        foreach (var item in items)
        {
            var serial = item.DispatchItem.SerialItem;
            if (!incomingIds.Contains(item.DispatchItemId) || serial.StockState != "IN_TRANSIT")
                return (false, $"Serial {serial.SerialNumber} không còn đang chuyển đến theo lệnh này.");
            if (item.ReceivedCondition is not ("GOOD" or "DAMAGED"))
                return (false, $"Serial {serial.SerialNumber} thiếu tình trạng thực nhận hợp lệ.");
        }

        var destination = await db.Warehouses.AsNoTracking()
            .Where(w => w.WarehouseId == order.ToWarehouseId)
            .Select(w => new { w.WarehouseType, w.RentalStartDate, w.RentalEndDate }).SingleAsync();
        var now = clock.GetUtcNow().UtcDateTime;
        var receivedOn = DateOnly.FromDateTime(now.AddHours(7));
        if (destination.WarehouseType == "RENTED")
        {
            if (destination.RentalStartDate is not DateOnly start || destination.RentalEndDate is not DateOnly end || start > end)
                return (false, "Kho đích chưa có kỳ thuê hợp lệ; chưa thể hoàn tất nhận hàng.");
            if (receivedOn < start)
                return (false, $"Kho đích bắt đầu thuê ngày {start:dd/MM/yyyy}; chưa thể nhận hàng ngày {receivedOn:dd/MM/yyyy}.");
            if (receivedOn > end)
                return (false, $"Kho đích đã hết hạn thuê ngày {end:dd/MM/yyyy}; không thể nhận hàng ngày {receivedOn:dd/MM/yyyy}.");
        }
        foreach (var group in items.GroupBy(i => new { i.DispatchItem.ProductId, Condition = i.ReceivedCondition! }))
        {
            var damaged = group.Key.Condition == "DAMAGED";
            var available = damaged ? 0 : group.Count();
            var source = new SupplySource
            {
                ProductId = group.Key.ProductId,
                WarehouseId = order.ToWarehouseId,
                ItemCondition = group.Key.Condition,
                SourceType = "TRANSFER_RECEIPT",
                SupplyState = "PHYSICAL",
                TransferReceiptId = receiptId,
                EligibleQuantity = available,
                RemainingQuantity = available,
                AvailableFrom = DateOnly.FromDateTime(now),
                CreatedAt = now
            };
            db.SupplySources.Add(source);
            await SaveAsActorAsync(userId);

            foreach (var item in group)
            {
                var serial = item.DispatchItem.SerialItem;
                db.SerialMovements.Add(new SerialMovement
                {
                    SerialItemId = serial.SerialItemId, TransferReceiptId = receiptId, PerformedByUserId = userId,
                    EventType = "TRANSFER_RECEIPT", OccurredAt = now, FromState = serial.StockState,
                    ToState = "IN_WAREHOUSE", FromCondition = serial.ItemCondition, ToCondition = group.Key.Condition,
                    FromQuarantined = serial.IsQuarantined, ToQuarantined = damaged, ToWarehouseId = order.ToWarehouseId
                });
                serial.StockState = "IN_WAREHOUSE";
                serial.CurrentWarehouseId = order.ToWarehouseId;
                serial.CurrentLocationId = null;
                serial.CurrentSupplySourceId = source.SupplySourceId;
                serial.CurrentSupplyState = "PHYSICAL";
                serial.ItemCondition = group.Key.Condition;
                serial.IsQuarantined = damaged;
                serial.QuarantineReason = damaged ? item.ConditionNote : null;
                if (damaged && string.IsNullOrWhiteSpace(serial.QuarantineReason))
                    serial.QuarantineReason = "Hư hỏng khi nhận điều chuyển";
            }
        }

        await SaveAsActorAsync(userId);
        receipt.Status = "DONE";
        receipt.CompletedAt = now;
        await SaveAsActorAsync(userId);
        await transaction.CommitAsync();
        return (true, $"Đã nhận {items.Count} serial trên phiếu {receipt.ReceiptCode}.");
    }

    public async Task<(bool Success, string Message)> RemoveSerialAsync(long orderId, long receiptId, long itemId, int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await LockDestinationOrderAsync(orderId, userId);
        if (order is null || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Lệnh không cho phép chỉnh sửa tại kho của bạn.");

        var item = await db.TransferReceiptItems.SingleOrDefaultAsync(i => i.TransferReceiptItemId == itemId
            && i.TransferReceiptId == receiptId && i.TransferOrderId == orderId
            && i.TransferReceipt.Status == "DRAFT" && i.TransferReceipt.PerformedByUserId == userId);
        if (item is null)
            return (false, "Bạn chỉ có thể gỡ serial trên phiếu nhận nháp mình phụ trách.");
        if (!item.IsActive)
            return (true, "Serial này đã được gỡ khỏi phiếu.");

        item.IsActive = false;
        await SaveAsActorAsync(userId);
        await tx.CommitAsync();
        return (true, "Đã gỡ serial khỏi phiếu nhận.");
    }

    private async Task SaveAsActorAsync(int userId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={userId}");
        try
        {
            await db.SaveChangesAsync();
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL");
        }
    }
}
