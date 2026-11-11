using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public class ManagerTransferRepository(EwmsDbContext db)
    : IManagerTransferRepository
{
    private IQueryable<TransferOrder> Filter(string? search, string? status)
    {
        var query = db.TransferOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(order =>
                order.TransferCode.Contains(search) ||
                order.FromWarehouse.WarehouseName.Contains(search) ||
                order.ToWarehouse.WarehouseName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(order => order.Status == status);
        }

        return query;
    }

    public Task<int> CountAsync(string? search, string? status) =>
        Filter(search, status).CountAsync();

    public Task<List<ManagerTransferSummary>> GetListAsync(string? search, string? status, int skip, int take)
    {
        return Filter(search, status)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.TransferOrderId)
            .Skip(skip)
            .Take(take)
            .Select(order => new ManagerTransferSummary(
                order.TransferOrderId,
                order.TransferCode,
                order.FromWarehouse.WarehouseName,
                order.ToWarehouse.WarehouseName,
                order.Status,
                order.CreatedAt,

                order.TransferOrderLines
                    .Sum(line => (long)line.RequestedQuantity),

                order.TransferOrderLines
                    .Sum(line => (long)line.CancelledQuantity),

                order.TransferDispatchItems
                    .LongCount(item =>
                        item.IsActive &&
                        item.Dispatch.Status == "DONE"),

                db.TransferReceiptItems
                    .LongCount(item =>
                        item.TransferOrderId == order.TransferOrderId &&
                        item.IsActive &&
                        item.TransferReceipt.Status == "DONE"),

                db.VwTransferInTransits
                    .LongCount(item =>
                        item.TransferOrderId == order.TransferOrderId)
            ))
            .ToListAsync();
    }

    public async Task<List<string>> GetStatusOptionsAsync()
    {
        const string sql = """
            SELECT definition AS [Value]
            FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.TransferOrder')
              AND name = N'CK_TransferOrder_03'
            """;

        var definition = await db.Database.SqlQueryRaw<string>(sql).SingleAsync();
        string[] lifecycleOrder =
            ["DRAFT", "RELEASED", "IN_PROGRESS", "COMPLETED", "CLOSED", "CANCELLED"];

        return Regex.Matches(definition, @"'([A-Z_]+)'")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .OrderBy(status =>
            {
                var index = Array.IndexOf(lifecycleOrder, status);
                return index >= 0 ? index : int.MaxValue;
            })
            .ToList();
    }

    public async Task<ManagerTransferDetail?> GetDetailAsync(long id)
    {
        var detail = await db.TransferOrders.AsNoTracking()
            .Where(order => order.TransferOrderId == id)
            .Select(order => new ManagerTransferDetail
            {
                TransferCode = order.TransferCode,
                FromWarehouse = order.FromWarehouse.WarehouseName,
                ToWarehouse = order.ToWarehouse.WarehouseName,
                Status = order.Status,
                ItemCondition = order.ItemCondition,
                AssignedTo = order.AssignedToUser == null ? null : order.AssignedToUser.FullName,
                DispatchDate = order.DispatchDate,
                ExpectedDate = order.ExpectedDate,
                Note = order.Note
            })
            .SingleOrDefaultAsync();

        if (detail is null)
        {
            return null;
        }

        detail.Lines = await db.TransferOrderLines.AsNoTracking()
            .Where(line => line.TransferOrderId == id)
            .OrderBy(line => line.TransferLineId)
            .Select(line => new ManagerTransferLine(
                line.Product.ProductCode,
                line.Product.ProductName,
                line.RequestedQuantity,
                line.CancelledQuantity,
                db.TransferDispatchItems.LongCount(item =>
                    item.TransferLineId == line.TransferLineId &&
                    item.IsActive && item.Dispatch.Status == "DONE"),
                db.TransferReceiptItems.LongCount(item =>
                    item.DispatchItem.TransferLineId == line.TransferLineId &&
                    item.IsActive && item.TransferReceipt.Status == "DONE"),
                db.VwTransferInTransits.LongCount(item =>
                    item.TransferLineId == line.TransferLineId)))
            .ToListAsync();

        detail.Dispatches = await db.TransferDispatches.AsNoTracking()
            .Where(dispatch => dispatch.TransferOrderId == id)
            .OrderBy(dispatch => dispatch.DispatchDate)
            .ThenBy(dispatch => dispatch.DispatchId)
            .Select(dispatch => new ManagerTransferDocument(
                dispatch.DispatchCode, dispatch.DispatchDate, dispatch.Status,
                dispatch.AssignedToUser == null ? null : dispatch.AssignedToUser.FullName))
            .ToListAsync();

        detail.Receipts = await db.TransferReceipts.AsNoTracking()
            .Where(receipt => receipt.TransferOrderId == id)
            .OrderBy(receipt => receipt.ReceiptDate)
            .ThenBy(receipt => receipt.TransferReceiptId)
            .Select(receipt => new ManagerTransferDocument(
                receipt.ReceiptCode, receipt.ReceiptDate, receipt.Status,
                receipt.PerformedByUser == null ? null : receipt.PerformedByUser.FullName))
            .ToListAsync();

        return detail;
    }
}
