using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public class TransferDispatchWorkService(EwmsDbContext db, TransferExpiryService expiry)
{
    private IQueryable<TransferOrder> SourceOrderForUpdate(long orderId, int userId) =>
        db.TransferOrders
            .FromSqlInterpolated($"SELECT * FROM dbo.TransferOrder WITH (UPDLOCK, HOLDLOCK) WHERE TransferOrderID = {orderId}")
            .Where(o => db.Users.Any(u => u.UserId == userId && u.Status == "ACTIVE"
                && u.Role.RoleName == "WAREHOUSE_STAFF" && u.WarehouseId == o.FromWarehouseId));

    public async Task<(bool Success, string Message)> ClaimAsync(long orderId, int userId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Lock the order even when no dispatch exists yet so concurrent claims use the same draft.
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();

        if (order is null)
            return (false, "Bạn chỉ có thể nhận việc xuất tại kho nguồn của mình.");
        if (await expiry.CancelLockedAsync(order))
        {
            await transaction.CommitAsync();
            return (false, "Lệnh đã quá ngày dự kiến đến và tự động hủy. Không thể tạo phiếu gửi; hãy yêu cầu quản lý tạo lệnh mới.");
        }
        if (order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Trạng thái hiện tại của lệnh chưa cho phép nhận việc xuất.");

        var hasRemaining = await db.TransferOrderLines.AnyAsync(line => line.TransferOrderId == orderId
            && line.RequestedQuantity - line.CancelledQuantity > db.TransferDispatchItems.Count(item =>
                item.TransferLineId == line.TransferLineId && item.IsActive && item.Dispatch.Status == "DONE"));
        if (!hasRemaining)
            return (false, "Lệnh đã xuất đủ số lượng.");

        var openDispatches = await db.TransferDispatches
            .Where(d => d.TransferOrderId == orderId && (d.Status == "DRAFT" || d.IsActivePicking))
            .OrderBy(d => d.DispatchId).Take(2).ToListAsync();
        if (openDispatches.Count > 1)
            return (false, "Lệnh có nhiều phiếu đang mở, chưa thể tự nhận đợt xuất.");

        var dispatch = openDispatches.SingleOrDefault();
        if (dispatch?.AssignedToUserId == userId)
            return (true, $"Bạn đang phụ trách phiếu {dispatch.DispatchCode}.");
        if (dispatch?.AssignedToUserId is not null)
            return (false, "Đợt xuất này đã được nhân viên khác nhận.");

        if (dispatch is null)
        {
            dispatch = new TransferDispatch
            {
                TransferOrderId = orderId,
                DispatchCode = $"TD-{Guid.NewGuid():N}",
                DispatchDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedByUserId = userId,
                Status = "DRAFT"
            };
            db.TransferDispatches.Add(dispatch);
        }

        dispatch.AssignedToUserId = userId;
        await SaveAsActorAsync(userId);
        await transaction.CommitAsync();
        return (true, $"Bạn đã nhận phiếu xuất {dispatch.DispatchCode}.");
    }

    public async Task<(bool Success, string Message)> ScanAsync(
        long orderId, long dispatchId, string? serialNumber, int userId)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return (false, "Vui lòng nhập serial.");

        var number = serialNumber.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();
        if (order is null || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Lệnh chưa sẵn sàng xuất tại kho của bạn.");

        var canScan = await db.TransferDispatches.AnyAsync(d => d.TransferOrderId == orderId
            && d.DispatchId == dispatchId && d.AssignedToUserId == userId && d.Status == "DRAFT");
        if (!canScan)
            return (false, "Bạn chỉ có thể quét vào phiếu nháp mình đã nhận.");

        // Serialize claims on this physical unit across transfer orders.
        var serial = await db.SerialItems
            .FromSqlInterpolated($"SELECT * FROM dbo.SerialItem WITH (UPDLOCK, HOLDLOCK) WHERE SerialNumber = {number}")
            .SingleOrDefaultAsync();
        if (serial is null || serial.CurrentWarehouseId != order.FromWarehouseId
            || serial.StockState != "IN_WAREHOUSE" || serial.IsQuarantined
            || serial.ItemCondition != order.ItemCondition || serial.CurrentSupplyState != "PHYSICAL"
            || serial.CurrentSupplySourceId is not long sourceId)
            return (false, "Serial không thuộc tồn kho nguồn đủ điều kiện xuất.");

        var claim = await db.PickingClaims.FindAsync(serial.SerialItemId);
        if (claim != null)
            return (false, claim.DispatchId == dispatchId ? "Serial đã được quét vào phiếu này."
                : "Serial đang được giữ cho phiếu khác.");

        var line = await db.TransferOrderLines.SingleOrDefaultAsync(l =>
            l.TransferOrderId == orderId && l.ProductId == serial.ProductId);
        if (line is null)
            return (false, "Sản phẩm của serial không thuộc lệnh chuyển kho.");

        var lineItems = db.TransferDispatchItems.Where(i => i.TransferLineId == line.TransferLineId && i.IsActive);
        var openItems = lineItems.Where(i => i.Dispatch.Status == "DRAFT" || i.Dispatch.IsActivePicking);
        var counted = await lineItems.CountAsync(i => i.Dispatch.Status == "DONE"
            || i.Dispatch.Status == "DRAFT" || i.Dispatch.IsActivePicking);
        if (counted >= line.RequestedQuantity - line.CancelledQuantity)
            return (false, "Dòng sản phẩm đã đủ số lượng.");

        var reserved = await db.Reservations.Where(r => r.TransferLineId == line.TransferLineId
            && r.SupplySourceId == sourceId && r.Status == "ACTIVE" && r.SupplyState == "PHYSICAL")
            .SumAsync(r => (int?)(r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity)) ?? 0;
        if (reserved <= await openItems.CountAsync(i => i.SourceAtDispatchId == sourceId))
            return (false, "Nguồn tồn của serial không còn số lượng giữ chỗ cho dòng này.");

        var now = DateTime.UtcNow;
        db.PickingClaims.Add(new PickingClaim
        {
            SerialItemId = serial.SerialItemId, DispatchId = dispatchId,
            ClaimedByUserId = userId, ClaimedAt = now
        });
        db.TransferDispatchItems.Add(new TransferDispatchItem
        {
            DispatchId = dispatchId, TransferOrderId = orderId, TransferLineId = line.TransferLineId,
            SerialItemId = serial.SerialItemId, ProductId = serial.ProductId, WarehouseId = order.FromWarehouseId,
            SourceAtDispatchId = sourceId, SupplyState = "PHYSICAL", DispatchedCondition = serial.ItemCondition,
            ScannedAt = now, IsActive = true
        });
        try
        {
            await SaveAsActorAsync(userId);
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return (false, "Serial đã được quét hoặc đang được giữ cho phiếu khác.");
        }
        return (true, $"Đã thêm serial {serial.SerialNumber} vào phiếu xuất nháp.");
    }

    public async Task<(bool Success, string Message)> CompleteAsync(long orderId, long dispatchId, int userId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();
        if (order is null)
            return (false, "Lệnh không thuộc kho nguồn của bạn.");

        var dispatch = await db.TransferDispatches.SingleOrDefaultAsync(d =>
            d.DispatchId == dispatchId && d.TransferOrderId == orderId && d.AssignedToUserId == userId);
        if (dispatch is null)
            return (false, "Bạn chỉ có thể hoàn tất phiếu xuất mình phụ trách.");
        if (dispatch.Status == "DONE")
            return (true, $"Phiếu {dispatch.DispatchCode} đã hoàn tất.");
        if (dispatch.Status != "DRAFT" || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Trạng thái hiện tại chưa cho phép hoàn tất phiếu.");

        var items = await db.TransferDispatchItems
            .Include(i => i.SerialItem).Include(i => i.TransferLine).Include(i => i.SourceAtDispatch)
            .Where(i => i.DispatchId == dispatchId && i.IsActive)
            .ToListAsync();
        if (items.Count == 0)
            return (false, "Hãy quét ít nhất một serial trước khi hoàn tất xuất.");

        var claims = await db.PickingClaims.Where(c => c.DispatchId == dispatchId).ToListAsync();
        var claimedIds = claims.Select(c => c.SerialItemId).ToHashSet();
        foreach (var item in items)
        {
            var serial = item.SerialItem;
            if (!claimedIds.Contains(serial.SerialItemId) || serial.CurrentWarehouseId != order.FromWarehouseId
                || serial.StockState != "IN_WAREHOUSE" || serial.CurrentSupplyState != "PHYSICAL"
                || serial.IsQuarantined || serial.ItemCondition != order.ItemCondition
                || serial.CurrentSupplySourceId != item.SourceAtDispatchId || serial.ProductId != item.TransferLine.ProductId)
                return (false, $"Serial {serial.SerialNumber} không còn đủ điều kiện xuất trên phiếu này.");
        }
        if (claims.Count != items.Count)
            return (false, "Danh sách serial giữ cho phiếu không khớp với các serial đã quét.");

        var alreadySent = await db.TransferDispatchItems
            .Where(i => i.TransferOrderId == orderId && i.IsActive && i.Dispatch.Status == "DONE")
            .GroupBy(i => i.TransferLineId)
            .Select(g => new { LineId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.LineId, g => g.Count);
        foreach (var group in items.GroupBy(i => i.TransferLine))
        {
            var line = group.Key;
            if (group.Count() + alreadySent.GetValueOrDefault(line.TransferLineId) > line.RequestedQuantity - line.CancelledQuantity)
                return (false, "Số lượng trên phiếu vượt quá số còn phải xuất.");
        }

        var reservations = await db.Reservations
            .Where(r => r.TransferOrderId == orderId && r.Status == "ACTIVE" && r.SupplyState == "PHYSICAL")
            .OrderBy(r => r.ReservationId).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var sourceGroup in items.GroupBy(i => i.SourceAtDispatch))
        {
            var source = sourceGroup.Key;
            var quantity = sourceGroup.Count();
            if (source.RemainingQuantity < quantity || source.EligibleQuantity < quantity)
                return (false, "Nguồn tồn không còn đủ số lượng để xuất phiếu.");

            var allocation = new DemandAllocation
            {
                DispatchId = dispatchId, TransferOrderId = orderId, SupplySourceId = source.SupplySourceId,
                Quantity = quantity, AllocatedAt = now
            };
            foreach (var lineGroup in sourceGroup.GroupBy(i => i.TransferLineId))
            {
                var needed = lineGroup.Count();
                foreach (var reservation in reservations.Where(r =>
                    r.TransferLineId == lineGroup.Key && r.SupplySourceId == source.SupplySourceId))
                {
                    var available = reservation.ReservedQuantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity;
                    if (available <= 0) continue;
                    var take = Math.Min(needed, available);
                    allocation.ReservationConsumptionAllocations.Add(new ReservationConsumption
                    {
                        ReservationId = reservation.ReservationId, SupplySourceId = source.SupplySourceId,
                        TransferOrderId = orderId, ConsumedQuantity = take
                    });
                    reservation.ConsumedQuantity += take;
                    if (take == available)
                    {
                        reservation.Status = "CLOSED";
                        reservation.ClosedAt = now;
                    }
                    needed -= take;
                    if (needed == 0) break;
                }
                if (needed != 0)
                    return (false, "Phần giữ chỗ của dòng hàng không đủ tại nguồn tồn đã quét.");
            }
            db.DemandAllocations.Add(allocation);
            source.RemainingQuantity -= quantity;
            source.EligibleQuantity -= quantity;
        }

        foreach (var item in items)
        {
            var serial = item.SerialItem;
            db.SerialMovements.Add(new SerialMovement
            {
                SerialItemId = serial.SerialItemId, DispatchId = dispatchId, PerformedByUserId = userId,
                EventType = "TRANSFER_DISPATCH", OccurredAt = now, FromState = serial.StockState, ToState = "IN_TRANSIT",
                FromCondition = serial.ItemCondition, ToCondition = serial.ItemCondition,
                FromWarehouseId = serial.CurrentWarehouseId, FromLocationId = serial.CurrentLocationId,
                FromQuarantined = serial.IsQuarantined, ToQuarantined = false
            });
            serial.StockState = "IN_TRANSIT";
            serial.CurrentWarehouseId = null;
            serial.CurrentLocationId = null;
            serial.CurrentSupplySourceId = null;
            serial.CurrentSupplyState = null;
        }
        db.PickingClaims.RemoveRange(claims);
        dispatch.Status = "DONE";
        dispatch.CompletedAt = now;
        dispatch.IsActivePicking = false;
        order.Status = "IN_PROGRESS";
        await SaveAsActorAsync(userId);
        await transaction.CommitAsync();
        return (true, $"Đã xuất {items.Count} serial trên phiếu {dispatch.DispatchCode}.");
    }

    public async Task<(bool Success, string Message)> RemoveSerialAsync(long orderId, long dispatchId, long itemId, int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();
        if (order is null || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Lệnh không cho phép chỉnh sửa tại kho của bạn.");

        var item = await db.TransferDispatchItems.SingleOrDefaultAsync(i => i.DispatchItemId == itemId
            && i.DispatchId == dispatchId && i.TransferOrderId == orderId
            && i.Dispatch.Status == "DRAFT" && i.Dispatch.AssignedToUserId == userId);
        if (item is null)
            return (false, "Bạn chỉ có thể gỡ serial trên phiếu xuất nháp mình phụ trách.");
        if (!item.IsActive)
            return (true, "Serial này đã được gỡ khỏi phiếu.");

        item.IsActive = false;
        var claim = await db.PickingClaims.SingleOrDefaultAsync(c =>
            c.DispatchId == dispatchId && c.SerialItemId == item.SerialItemId);
        if (claim != null) db.PickingClaims.Remove(claim);
        await SaveAsActorAsync(userId);
        await tx.CommitAsync();
        return (true, "Đã gỡ serial khỏi phiếu xuất.");
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
