using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Repositories.Stocktaking;
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
                line.Product.ProductCode, line.Product.ProductName, line.RequestedQuantity, line.CancelledQuantity,
                db.TransferDispatchItems.Count(item => item.TransferLineId == line.TransferLineId && item.IsActive && item.Dispatch.Status == "DONE"),
                db.TransferReceiptItems.Count(item => item.DispatchItem.TransferLineId == line.TransferLineId && item.IsActive && item.TransferReceipt.Status == "DONE"),
                db.VwTransferInTransits.Count(item => item.TransferLineId == line.TransferLineId))
            {
                ProductId = line.ProductId,
                Picking = db.TransferDispatchItems.Count(item => item.TransferLineId == line.TransferLineId && item.IsActive
                    && (item.Dispatch.Status == "DRAFT" || item.Dispatch.IsActivePicking))
            }).ToListAsync();

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

            var locationMap = (await db.StorageLocations.AsNoTracking().Where(location => location.WarehouseId == warehouseId).ToListAsync())
                .ToDictionary(location => location.LocationId);
            detail.PickLocations = locationMap.ToDictionary(entry => entry.Key, entry => LocationPath(locationMap, entry.Key));

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
                            ProductName = item.TransferLine.Product.ProductName,
                            ItemId = item.DispatchItemId,
                            LocationId = dispatch.Status == "DONE"
                                ? db.SerialMovements.Where(m => m.DispatchId == dispatch.DispatchId
                                    && m.SerialItemId == item.SerialItemId && m.EventType == "TRANSFER_DISPATCH")
                                    .OrderByDescending(m => m.MovementId).Select(m => m.FromLocationId).FirstOrDefault()
                                : item.SerialItem.CurrentLocationId
                        })
                        .ToList())
                {
                    DispatchId = dispatch.DispatchId,
                    CanScan = dispatch.AssignedToUserId == userId && dispatch.Status == "DRAFT"
                        && (dispatch.TransferOrder.Status == "RELEASED" || dispatch.TransferOrder.Status == "IN_PROGRESS")
                })
                .ToListAsync();

            if (detail.Documents.Any(doc => doc.CanScan))
            {
                var active = db.TransferDispatchItems.Where(item => item.IsActive);
                var open = active.Where(item => item.Dispatch.Status == "DRAFT" || item.Dispatch.IsActivePicking);
                var remainingLines = db.TransferOrderLines.Where(line => line.TransferOrderId == orderId
                    && active.Count(item => item.TransferLineId == line.TransferLineId
                        && (item.Dispatch.Status == "DONE" || item.Dispatch.Status == "DRAFT" || item.Dispatch.IsActivePicking))
                        < line.RequestedQuantity - line.CancelledQuantity);
                var options = await (
                    from line in remainingLines
                    join serial in db.SerialItems on line.ProductId equals serial.ProductId
                    where serial.CurrentWarehouseId == warehouseId && serial.StockState == "IN_WAREHOUSE"
                        && serial.ItemCondition == detail.ItemCondition && serial.CurrentSupplyState == "PHYSICAL"
                        && serial.CurrentSupplySourceId != null && !db.PickingClaims.Any(claim => claim.SerialItemId == serial.SerialItemId)
                    let reserved = db.Reservations.Where(reservation => reservation.TransferLineId == line.TransferLineId
                        && reservation.SupplySourceId == serial.CurrentSupplySourceId && reservation.Status == "ACTIVE" && reservation.SupplyState == "PHYSICAL")
                        .Sum(reservation => (int?)(reservation.ReservedQuantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity)) ?? 0
                    let sourceRemaining = reserved - open.Count(item => item.TransferLineId == line.TransferLineId && item.SourceAtDispatchId == serial.CurrentSupplySourceId)
                    where sourceRemaining > 0
                    orderby line.Product.ProductCode, serial.ManufacturingYear, serial.ManufacturingMonth, serial.SerialNumber
                    select new DispatchSerialOption(line.Product.ProductCode, line.Product.ProductName, "", serial.SerialNumber,
                        serial.ManufacturingMonth, serial.ManufacturingYear, serial.ItemCondition)
                    {
                        ProductId = serial.ProductId, SupplySourceId = serial.CurrentSupplySourceId!.Value,
                        LocationId = serial.CurrentLocationId, SourceRemaining = sourceRemaining,
                        LineRemaining = line.RequestedQuantity - line.CancelledQuantity - active.Count(item => item.TransferLineId == line.TransferLineId
                            && (item.Dispatch.Status == "DONE" || item.Dispatch.Status == "DRAFT" || item.Dispatch.IsActivePicking)),
                        SourceCode = serial.CurrentSupplySource!.Inbound != null ? "Đã nhận · " + serial.CurrentSupplySource!.Inbound!.ReceiptCode
                            : serial.CurrentSupplySource!.ReturnReceipt != null ? "Nhận trả · " + serial.CurrentSupplySource!.ReturnReceipt!.ReceiptCode
                            : serial.CurrentSupplySource!.TransferReceipt != null ? "Nhận chuyển · " + serial.CurrentSupplySource!.TransferReceipt!.ReceiptCode
                            : "Nguồn #" + serial.CurrentSupplySourceId
                    }).ToListAsync();
                var plans = await db.Stocktakes.AsNoTracking().Where(plan => plan.WarehouseId == warehouseId
                    && StocktakeScopeRules.LockedStatuses.Contains(plan.Status)).ToListAsync();
                var planIds = plans.Select(plan => plan.StocktakeId).ToArray();
                var scopes = await db.StocktakeScopes.AsNoTracking().Where(scope => planIds.Contains(scope.StocktakeId)).ToListAsync();
                detail.DispatchSerialOptions = options.Where(option => StocktakeScopeRules.MovementBlockReason(
                        warehouseId.Value, plans, scopes, locationMap.Values.ToList(), [option.LocationId]) is null)
                    .Select(option => option with { Location = option.LocationId.HasValue
                        ? detail.PickLocations.GetValueOrDefault(option.LocationId.Value, "Vị trí không còn trong cấu trúc kho")
                        : "Hàng chờ xếp kho" }).ToList();
                detail.Lines = detail.Lines.Select(line => line with
                {
                    ReadyToPick = (int)Math.Min(Math.Max(0, line.RemainingToDispatch - line.Picking),
                        detail.DispatchSerialOptions.Where(option => option.ProductId == line.ProductId)
                            .GroupBy(option => option.SupplySourceId).Sum(group => Math.Min(group.Count(), group.First().SourceRemaining)))
                }).ToList();
                detail.IsDispatchQuantityCovered = !await remainingLines.AnyAsync();
            }
        }
        else
        {
            detail.IncomingSerials = await db.VwTransferInTransits
                .Where(item => item.TransferOrderId == orderId && item.ToWarehouseId == warehouseId)
                .OrderBy(item => item.SerialNumber)
                .Select(item => item.SerialNumber)
                .ToListAsync();

            detail.ReceiptSerialOptions = await (
                from incoming in db.VwTransferInTransits
                join serial in db.SerialItems on incoming.SerialItemId equals serial.SerialItemId
                join product in db.Products on incoming.ProductId equals product.ProductId
                where incoming.TransferOrderId == orderId && incoming.ToWarehouseId == warehouseId
                orderby product.ProductCode, serial.SerialNumber
                select new ReceiptSerialOption(serial.SerialNumber, product.ProductCode, product.ProductName,
                    incoming.DispatchedCondition, serial.ManufacturingMonth, serial.ManufacturingYear)
                    { ProductId = product.ProductId }).ToListAsync();

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
                            ProductName = item.DispatchItem.TransferLine.Product.ProductName,
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
    private static string LocationPath(IReadOnlyDictionary<int, PRN222_EWMS_Web.Models.StorageLocation> locations, int id)
    {
        var names = new List<string>();
        var seen = new HashSet<int>();
        int? current = id;
        while (current is int locationId && seen.Add(locationId) && locations.TryGetValue(locationId, out var location))
        {
            names.Insert(0, location.LocationName);
            current = location.ParentLocationId;
        }
        return current.HasValue || names.Count == 0 ? "Vị trí không còn trong cấu trúc kho" : string.Join(" → ", names);
    }
}
