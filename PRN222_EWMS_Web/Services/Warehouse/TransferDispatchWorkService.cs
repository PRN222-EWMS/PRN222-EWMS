using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public class TransferDispatchWorkService(EwmsDbContext db, TransferExpiryService expiry, ISourceReservationService commitments,
    DocumentCodeGenerator codes)
{
    private async Task<int[]> WorkWarehousesAsync(long orderId, int userId)
    {
        var context = await db.TransferOrders.AsNoTracking().Where(o => o.TransferOrderId == orderId
            && db.Users.Any(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF"
                && u.WarehouseId == o.FromWarehouseId))
            .Select(o => new { o.FromWarehouseId, o.ToWarehouseId }).SingleOrDefaultAsync();
        return context is null ? [] : [context.FromWarehouseId, context.ToWarehouseId];
    }

    private async Task LockWarehousesAsync(int[] ids)
    {
        foreach (var id in ids.Distinct().Order()) await commitments.LockWarehouseAsync(id);
    }

    private IQueryable<TransferOrder> SourceOrderForUpdate(long orderId, int userId) =>
        db.TransferOrders
            .FromSqlInterpolated($"SELECT * FROM dbo.TransferOrder WITH (UPDLOCK, HOLDLOCK) WHERE TransferOrderID = {orderId}")
            .Where(o => db.Users.Any(u => u.UserId == userId && u.Status == "ACTIVE"
                && u.Role.RoleName == "WAREHOUSE_STAFF" && u.WarehouseId == o.FromWarehouseId));

    public async Task<(bool Success, string Message)> ClaimAsync(long orderId, int userId)
    {
        var warehouses = await WorkWarehousesAsync(orderId, userId);
        if (warehouses.Length == 0) return (false, "Lệnh không thuộc kho nguồn của bạn.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockWarehousesAsync(warehouses);

        // Lock the order even when no dispatch exists yet so concurrent claims use the same draft.
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();

        if (order is null || order.FromWarehouseId != warehouses[0] || order.ToWarehouseId != warehouses[1])
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
                DispatchCode = await codes.NextAsync(DocumentType.TransferDispatch),
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

    public Task<(bool Success, string Message)> ScanAsync(long orderId, long dispatchId, string? serialNumber, int userId) =>
        ScanBatchAsync(orderId, dispatchId, [serialNumber ?? ""], userId);

    public async Task<(bool Success, string Message)> ScanBatchAsync(long orderId, long dispatchId,
        IReadOnlyCollection<string> serialNumbers, int userId)
    {
        if (serialNumbers.Count is < 1 or > 1000)
            return (false, "Chọn từ 1 đến 1.000 serial mỗi lượt.");
        var numbers = serialNumbers.Select(number => number?.Trim() ?? "").ToArray();
        if (numbers.Any(number => number.Length is < 1 or > 100 || number.Any(char.IsControl)))
            return (false, "Serial phải dài 1–100 ký tự và không chứa ký tự điều khiển.");
        if (numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != numbers.Length)
            return (false, "Danh sách có serial trùng.");

        var warehouses = await WorkWarehousesAsync(orderId, userId);
        if (warehouses.Length == 0) return (false, "Lệnh không thuộc kho nguồn của bạn.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockWarehousesAsync(warehouses);
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();
        if (order is null || order.FromWarehouseId != warehouses[0] || order.ToWarehouseId != warehouses[1]
            || order.Status is not ("RELEASED" or "IN_PROGRESS"))
            return (false, "Lệnh chưa sẵn sàng xuất tại kho của bạn.");
        if (!await db.TransferDispatches.AnyAsync(dispatch => dispatch.TransferOrderId == orderId
            && dispatch.DispatchId == dispatchId && dispatch.AssignedToUserId == userId && dispatch.Status == "DRAFT"))
            return (false, "Bạn chỉ có thể quét vào phiếu nháp mình đã nhận.");

        var serials = new List<SerialItem>();
        foreach (var number in numbers.Order(StringComparer.OrdinalIgnoreCase))
        {
            var serial = await db.SerialItems
                .FromSqlInterpolated($"SELECT * FROM dbo.SerialItem WITH (UPDLOCK, HOLDLOCK) WHERE SerialNumber = {number}")
                .SingleOrDefaultAsync();
            if (serial is null || serial.CurrentWarehouseId != order.FromWarehouseId || serial.StockState != "IN_WAREHOUSE"
                || serial.ItemCondition != order.ItemCondition || serial.CurrentSupplyState != "PHYSICAL"
                || serial.CurrentSupplySourceId is null)
                return (false, $"Serial {number} không thuộc tồn kho nguồn đủ điều kiện xuất.");
            serials.Add(serial);
        }
        if (serials.Select(serial => serial.SerialItemId).Distinct().Count() != serials.Count)
            return (false, "Danh sách đang trỏ đến cùng một serial nhiều lần.");
        var block = await commitments.GetStocktakeBlockReasonAsync(order.FromWarehouseId,
            serials.Select(serial => serial.CurrentLocationId).Distinct().ToArray());
        if (block is not null) return (false, block);
        var serialIds = serials.Select(serial => serial.SerialItemId).ToArray();
        if (await db.PickingClaims.AnyAsync(claim => serialIds.Contains(claim.SerialItemId)))
            return (false, "Có serial đang được phiếu này hoặc phiên khác giữ. Chưa thêm danh sách; hãy cập nhật lại.");

        var lines = await db.TransferOrderLines.Where(line => line.TransferOrderId == orderId).ToDictionaryAsync(line => line.ProductId);
        if (serials.Any(serial => !lines.ContainsKey(serial.ProductId)))
            return (false, "Sản phẩm của serial không thuộc lệnh chuyển kho.");
        foreach (var group in serials.GroupBy(serial => serial.ProductId))
        {
            var line = lines[group.Key];
            var lineItems = db.TransferDispatchItems.Where(item => item.TransferLineId == line.TransferLineId && item.IsActive);
            var used = await lineItems.CountAsync(item => item.Dispatch.Status == "DONE" || item.Dispatch.Status == "DRAFT" || item.Dispatch.IsActivePicking);
            if ((long)used + group.Count() > line.RequestedQuantity - line.CancelledQuantity)
                return (false, "Số lượng đã tích vượt phần còn phải xuất của sản phẩm.");
            foreach (var sourceGroup in group.GroupBy(serial => serial.CurrentSupplySourceId!.Value))
            {
                var reserved = await db.Reservations.Where(reservation => reservation.TransferLineId == line.TransferLineId
                    && reservation.SupplySourceId == sourceGroup.Key && reservation.Status == "ACTIVE" && reservation.SupplyState == "PHYSICAL")
                    .SumAsync(reservation => (long?)(reservation.ReservedQuantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity)) ?? 0;
                var picked = await lineItems.CountAsync(item => item.SourceAtDispatchId == sourceGroup.Key
                    && (item.Dispatch.Status == "DRAFT" || item.Dispatch.IsActivePicking));
                if ((long)picked + sourceGroup.Count() > reserved)
                    return (false, "Số lượng đã tích vượt phần nguồn được giữ cho lệnh.");
            }
        }
        var now = DateTime.UtcNow;
        foreach (var serial in serials)
        {
            db.PickingClaims.Add(new PickingClaim
            {
                SerialItemId = serial.SerialItemId, DispatchId = dispatchId, ClaimedByUserId = userId, ClaimedAt = now
            });
            db.TransferDispatchItems.Add(new TransferDispatchItem
            {
                DispatchId = dispatchId, TransferOrderId = orderId, TransferLineId = lines[serial.ProductId].TransferLineId,
                SerialItemId = serial.SerialItemId, ProductId = serial.ProductId, WarehouseId = order.FromWarehouseId,
                SourceAtDispatchId = serial.CurrentSupplySourceId!.Value, SupplyState = "PHYSICAL", DispatchedCondition = serial.ItemCondition,
                ScannedAt = now, IsActive = true
            });
        }
        try
        {
            await SaveAsActorAsync(userId);
            await transaction.CommitAsync();
            return (true, $"Đã thêm {serials.Count} serial vào phiếu xuất nháp.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return (false, "Có serial vừa được phiếu khác giữ. Chưa thêm danh sách; hãy cập nhật lại.");
        }
    }

    public async Task<(bool Success, string Message)> CompleteAsync(long orderId, long dispatchId, int userId)
    {
        var context = await db.TransferOrders.AsNoTracking().Where(o => o.TransferOrderId == orderId
                && db.Users.Any(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF"
                    && u.WarehouseId == o.FromWarehouseId))
            .Select(o => new { o.FromWarehouseId, o.ToWarehouseId }).SingleOrDefaultAsync();
        if (context == null) return (false, "Lệnh không thuộc kho làm việc của bạn.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var warehouseId in new[] { context.FromWarehouseId, context.ToWarehouseId }.Distinct().Order())
            await commitments.LockWarehouseAsync(warehouseId);
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();
        if (order is null || order.FromWarehouseId != context.FromWarehouseId || order.ToWarehouseId != context.ToWarehouseId)
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
                || serial.ItemCondition != order.ItemCondition
                || serial.CurrentSupplySourceId != item.SourceAtDispatchId || serial.ProductId != item.TransferLine.ProductId)
                return (false, $"Serial {serial.SerialNumber} không còn đủ điều kiện xuất trên phiếu này.");
        }
        if (claims.Count != items.Count)
            return (false, "Danh sách serial giữ cho phiếu không khớp với các serial đã quét.");
        var stocktakeBlock = await commitments.GetStocktakeBlockReasonAsync(order.FromWarehouseId,
            items.Select(i => i.SerialItem.CurrentLocationId).Distinct().ToArray());
        if (stocktakeBlock is not null) return (false, stocktakeBlock);

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
            if (source.RemainingQuantity < quantity)
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
            source.EligibleQuantity = Math.Min(source.RemainingQuantity, Math.Max(0, source.EligibleQuantity - quantity));
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
            serial.IsQuarantined = false;
            serial.QuarantineReason = null;
        }
        db.PickingClaims.RemoveRange(claims);
        dispatch.Status = "DONE";
        dispatch.CompletedAt = now;
        dispatch.IsActivePicking = false;
        order.Status = "IN_PROGRESS";
        await SaveAsActorAsync(userId);
        await SaveAsActorAsync(userId, () => commitments.AfterDispatchAsync(orderId));
        await transaction.CommitAsync();
        return (true, $"Đã xuất {items.Count} serial trên phiếu {dispatch.DispatchCode}.");
    }

    public async Task<(bool Success, string Message)> RemoveSerialAsync(long orderId, long dispatchId, long itemId, int userId)
    {
        var warehouses = await WorkWarehousesAsync(orderId, userId);
        if (warehouses.Length == 0) return (false, "Lệnh không thuộc kho nguồn của bạn.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockWarehousesAsync(warehouses);
        var order = await SourceOrderForUpdate(orderId, userId).SingleOrDefaultAsync();
        if (order is null || order.FromWarehouseId != warehouses[0] || order.ToWarehouseId != warehouses[1]
            || order.Status is not ("RELEASED" or "IN_PROGRESS"))
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

    private async Task SaveAsActorAsync(int userId, Func<Task>? sourceWork = null)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={userId}");
        try
        {
            if (sourceWork != null) await sourceWork();
            await db.SaveChangesAsync();
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL");
        }
    }
}
