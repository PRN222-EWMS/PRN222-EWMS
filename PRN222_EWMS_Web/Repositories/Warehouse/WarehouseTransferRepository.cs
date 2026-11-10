using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.WarehouseTransfer;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public class WarehouseTransferRepository(EwmsDbContext db)
{
    public Task<List<TransferListRow>> ListAsync(int userId)
    {
        var staffWarehouseIds = db.Users
            .Where(user => user.UserId == userId
                        && user.Status == "ACTIVE"
                        && user.Role.RoleName == "WAREHOUSE_STAFF")
            .Select(user => user.WarehouseId);

        var work = db.TransferDispatches
            .Where(d => staffWarehouseIds.Contains(d.TransferOrder.FromWarehouseId)
                && (d.Status == "DRAFT" || d.IsActivePicking))
            .Select(d => new { d.TransferOrderId, OwnerId = d.AssignedToUserId,
                OwnerName = d.AssignedToUser == null ? null : d.AssignedToUser.FullName, d.Status })
            .Concat(db.TransferReceipts
                .Where(r => staffWarehouseIds.Contains(r.TransferOrder.ToWarehouseId) && r.Status == "DRAFT")
                .Select(r => new { r.TransferOrderId, OwnerId = r.PerformedByUserId,
                    OwnerName = r.PerformedByUser == null ? null : r.PerformedByUser.FullName, r.Status }));

        return db.TransferOrders
            .AsNoTracking()
            .Where(order => staffWarehouseIds.Contains(order.FromWarehouseId)
                         || staffWarehouseIds.Contains(order.ToWarehouseId))
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.TransferOrderId)
            .Select(order => new TransferListRow(
                order.TransferOrderId,
                order.TransferCode,
                order.FromWarehouse.WarehouseName,
                order.ToWarehouse.WarehouseName,
                staffWarehouseIds.Contains(order.FromWarehouseId),
                order.Status,
                order.TransferDispatchItems.Count(item =>
                    item.IsActive && item.Dispatch.Status == "DONE"),
                db.TransferReceiptItems.Count(item =>
                    item.TransferOrderId == order.TransferOrderId
                    && item.IsActive
                    && item.TransferReceipt.Status == "DONE"),
                db.VwTransferInTransits.Count(item =>
                    item.TransferOrderId == order.TransferOrderId))
            {
                ViewerId = userId,
                OpenCount = work.Count(w => w.TransferOrderId == order.TransferOrderId),
                OwnerId = work.Where(w => w.TransferOrderId == order.TransferOrderId).Select(w => w.OwnerId).FirstOrDefault(),
                OwnerName = work.Where(w => w.TransferOrderId == order.TransferOrderId).Select(w => w.OwnerName).FirstOrDefault(),
                IsDraft = work.Any(w => w.TransferOrderId == order.TransferOrderId && w.Status == "DRAFT"),
                HasRemainingToDispatch = order.TransferOrderLines.Any(line =>
                    line.RequestedQuantity - line.CancelledQuantity > db.TransferDispatchItems.Count(item =>
                        item.TransferLineId == line.TransferLineId && item.IsActive && item.Dispatch.Status == "DONE"))
            })
            .ToListAsync();
    }

    public async Task<WarehouseTransferDetail?> GetDetailAsync(int userId, long orderId)
    {
        var warehouseId = await db.Users
            .Where(user => user.UserId == userId && user.Status == "ACTIVE"
                        && user.Role.RoleName == "WAREHOUSE_STAFF")
            .Select(user => user.WarehouseId)
            .SingleOrDefaultAsync();

        if (warehouseId is null)
            return null;

        var detail = await db.TransferOrders
            .Where(order => order.TransferOrderId == orderId
                         && (order.FromWarehouseId == warehouseId || order.ToWarehouseId == warehouseId))
            .Select(order => new WarehouseTransferDetail
            {
                Code = order.TransferCode,
                Id = order.TransferOrderId,
                FromWarehouse = order.FromWarehouse.WarehouseName,
                ToWarehouse = order.ToWarehouse.WarehouseName,
                Status = order.Status,
                ItemCondition = order.ItemCondition,
                IsSource = order.FromWarehouseId == warehouseId
            })
            .SingleOrDefaultAsync();

        if (detail is null)
            return null;

        detail.Lines = await db.TransferOrderLines
            .Where(line => line.TransferOrderId == orderId)
            .OrderBy(line => line.TransferLineId)
            .Select(line => new TransferProgressRow(
                line.Product.ProductCode, line.Product.ProductName,
                line.RequestedQuantity, line.CancelledQuantity,
                db.TransferDispatchItems.Count(item => item.TransferLineId == line.TransferLineId
                    && item.IsActive && item.Dispatch.Status == "DONE"),
                db.TransferReceiptItems.Count(item => item.DispatchItem.TransferLineId == line.TransferLineId
                    && item.IsActive && item.TransferReceipt.Status == "DONE"),
                db.VwTransferInTransits.Count(item => item.TransferLineId == line.TransferLineId)))
            .ToListAsync();

        if (detail.IsSource)
        {
            if ((detail.Status is "RELEASED" or "IN_PROGRESS") &&
                detail.Lines.Any(line => line.Requested - line.Cancelled > line.Dispatched))
            {
                var owners = await db.TransferDispatches
                    .Where(d => d.TransferOrderId == orderId && (d.Status == "DRAFT" || d.IsActivePicking))
                    .OrderBy(d => d.DispatchId)
                    .Select(d => d.AssignedToUserId)
                    .Take(2)
                    .ToListAsync();
                detail.CanClaimDispatch = owners.Count == 0 || (owners.Count == 1 && owners[0] is null);
            }

            detail.Documents = await db.TransferDispatches
                .Where(dispatch => dispatch.TransferOrderId == orderId)
                .OrderBy(dispatch => dispatch.DispatchId)
                .Select(dispatch => new WarehouseTransferDocument(
                    dispatch.DispatchCode, dispatch.DispatchDate, dispatch.Status,
                    dispatch.AssignedToUser == null ? null : dispatch.AssignedToUser.FullName,
                    dispatch.TransferDispatchItemDispatches
                        .Where(item => item.IsActive)
                        .OrderBy(item => item.DispatchItemId)
                        .Select(item => new WarehouseTransferSerial(item.TransferLine.Product.ProductCode,
                            item.SerialItem.SerialNumber, item.DispatchedCondition)
                        {
                            ItemId = item.DispatchItemId
                        })
                        .ToList())
                {
                    DispatchId = dispatch.DispatchId,
                    CanScan = dispatch.AssignedToUserId == userId && dispatch.Status == "DRAFT"
                        && (dispatch.TransferOrder.Status == "RELEASED" || dispatch.TransferOrder.Status == "IN_PROGRESS")
                })
                .ToListAsync();
        }
        else
        {
            detail.IncomingSerials = await db.VwTransferInTransits
                .Where(item => item.TransferOrderId == orderId && item.ToWarehouseId == warehouseId)
                .OrderBy(item => item.SerialNumber)
                .Select(item => item.SerialNumber)
                .ToListAsync();

            if ((detail.Status is "RELEASED" or "IN_PROGRESS") && detail.IncomingSerials.Count > 0)
            {
                var owners = await db.TransferReceipts
                    .Where(r => r.TransferOrderId == orderId && r.Status == "DRAFT")
                    .OrderBy(r => r.TransferReceiptId)
                    .Select(r => r.PerformedByUserId)
                    .Take(2).ToListAsync();
                detail.CanClaimReceipt = owners.Count == 0 || (owners.Count == 1 && owners[0] is null);
            }

            detail.Documents = await db.TransferReceipts
                .Where(receipt => receipt.TransferOrderId == orderId)
                .OrderBy(receipt => receipt.TransferReceiptId)
                .Select(receipt => new WarehouseTransferDocument(
                    receipt.ReceiptCode, receipt.ReceiptDate, receipt.Status,
                    receipt.PerformedByUser == null ? null : receipt.PerformedByUser.FullName,
                    receipt.TransferReceiptItemTransferReceipts
                        .Where(item => item.IsActive)
                        .OrderBy(item => item.TransferReceiptItemId)
                        .Select(item => new WarehouseTransferSerial(item.DispatchItem.TransferLine.Product.ProductCode,
                            item.DispatchItem.SerialItem.SerialNumber, item.ReceivedCondition)
                        {
                            ConditionNote = item.ConditionNote
                            , ItemId = item.TransferReceiptItemId
                        })
                        .ToList())
                {
                    ReceiptId = receipt.TransferReceiptId,
                    CanScanReceipt = receipt.PerformedByUserId == userId && receipt.Status == "DRAFT"
                        && (receipt.TransferOrder.Status == "RELEASED" || receipt.TransferOrder.Status == "IN_PROGRESS")
                })
                .ToListAsync();
        }

        return detail;
    }
}
