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
                    item.TransferOrderId == order.TransferOrderId)))
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
                            item.SerialItem.SerialNumber, item.DispatchedCondition))
                        .ToList()))
                .ToListAsync();
        }
        else
        {
            detail.IncomingSerials = await db.VwTransferInTransits
                .Where(item => item.TransferOrderId == orderId && item.ToWarehouseId == warehouseId)
                .OrderBy(item => item.SerialNumber)
                .Select(item => item.SerialNumber)
                .ToListAsync();

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
                            item.DispatchItem.SerialItem.SerialNumber, item.ReceivedCondition))
                        .ToList()))
                .ToListAsync();
        }

        return detail;
    }
}
