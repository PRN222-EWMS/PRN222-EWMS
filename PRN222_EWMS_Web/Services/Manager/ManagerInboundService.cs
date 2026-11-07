using PRN222_EWMS_Web.Repositories.Manager;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public class ManagerInboundService(IManagerInboundRepository repository) : IManagerInboundService
{
    private const int PageSize = 10;

    public async Task<ManagerInboundListViewModel> GetListAsync(ManagerInboundFilter filter)
    {
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        filter.Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim().ToUpperInvariant();
        if (filter.Search?.Length > 100 || filter.Status is not null and not ("DRAFT" or "DONE" or "CANCELLED")
            || filter.WarehouseId < 1 || filter.FromDate > filter.ToDate)
            throw new ArgumentException("Bộ lọc phiếu nhập không hợp lệ.");

        var count = await repository.CountAsync(filter);
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        filter.Page = Math.Clamp(filter.Page, 1, pages);
        var receipts = await repository.GetPageAsync(filter, filter.Page, PageSize);
        var progress = await repository.GetPoProgressAsync(receipts.Select(receipt => receipt.PurchaseOrderId));
        var byPo = progress.GroupBy(row => row.PurchaseOrderId).ToDictionary(group => group.Key, group => group.ToList());
        var rows = receipts.Select(receipt =>
        {
            byPo.TryGetValue(receipt.PurchaseOrderId, out var lines);
            var ordered = lines?.Sum(line => (long)line.OrderedQuantity)
                ?? receipt.PurchaseOrder.PurchaseOrderLines.Sum(line => (long)line.OrderedQuantity);
            var received = lines?.Sum(line => line.ReceivedQuantity) ?? 0;
            var closed = lines?.Sum(line => (long)line.ClosedQuantity)
                ?? receipt.PurchaseOrder.PurchaseOrderLines.Sum(line => (long)line.ClosedQuantity);
            var scanned = receipt.InboundItemInbounds.Count(item => item.IsActive);
            var accepted = receipt.Status == "DONE"
                ? receipt.InboundItemInbounds.Count(item => item.IsActive && item.SerialItemId.HasValue) : 0;
            return new ManagerInboundSummary(receipt.InboundId, receipt.ReceiptCode, receipt.PurchaseOrder.Pocode,
                receipt.PurchaseOrder.Supplier.SupplierName, receipt.PurchaseOrder.Warehouse.WarehouseName,
                receipt.Status, receipt.ReceiptDate, receipt.CreatedAt, scanned, accepted,
                ordered, received, Math.Max(0, ordered - received - closed), closed);
        }).ToList();
        return new ManagerInboundListViewModel
        {
            Filter = filter, TotalCount = count, TotalPages = pages, Receipts = rows,
            Warehouses = await repository.GetWarehousesAsync()
        };
    }

    public async Task<ManagerInboundDetailViewModel?> GetDetailAsync(long id)
    {
        var receipt = await repository.GetDetailAsync(id);
        if (receipt is null) return null;
        var progress = await repository.GetPoProgressAsync([receipt.PurchaseOrderId]);
        var byLine = progress.ToDictionary(row => row.PolineId);
        var activeItems = receipt.InboundItemInbounds.Where(item => item.IsActive).ToList();
        var acceptedItems = receipt.Status == "DONE"
            ? activeItems.Where(item => item.SerialItemId.HasValue).ToList() : [];
        var lines = receipt.PurchaseOrder.PurchaseOrderLines.OrderBy(line => line.Product.ProductCode).Select(line =>
        {
            byLine.TryGetValue(line.PolineId, out var row);
            var ordered = (long)line.OrderedQuantity;
            var received = row?.ReceivedQuantity ?? 0;
            var closed = (long)line.ClosedQuantity;
            return new ManagerInboundLine(line.Product.ProductCode, line.Product.ProductName,
                ordered, received, Math.Max(0, ordered - received - closed), closed,
                acceptedItems.Count(item => item.PolineId == line.PolineId),
                activeItems.Count(item => item.PolineId == line.PolineId));
        }).ToList();
        return new ManagerInboundDetailViewModel
        {
            Id = receipt.InboundId, ReceiptCode = receipt.ReceiptCode, PoCode = receipt.PurchaseOrder.Pocode,
            Supplier = receipt.PurchaseOrder.Supplier.SupplierName, Warehouse = receipt.PurchaseOrder.Warehouse.WarehouseName,
            Status = receipt.Status, ReceiptDate = receipt.ReceiptDate, CreatedAt = receipt.CreatedAt,
            CompletedAt = receipt.CompletedAt, CreatedBy = receipt.CreatedByUser.FullName,
            PerformedBy = receipt.PerformedByUser?.FullName, Note = receipt.Note, CancelReason = receipt.CancelReason,
            Scanned = activeItems.Count, Accepted = acceptedItems.Count,
            PoOrdered = lines.Sum(line => line.Ordered), PoReceived = lines.Sum(line => line.Received),
            PoOutstanding = lines.Sum(line => line.Outstanding), PoClosed = lines.Sum(line => line.Closed),
            Lines = lines,
            Serials = activeItems.OrderByDescending(item => item.ScannedAt).Select(item => new ManagerInboundSerial(
                item.ScannedSerial, item.Poline.Product.ProductCode, item.Poline.Product.ProductName,
                item.ManufacturingMonth, item.ManufacturingYear, item.ReceivedCondition, item.ConditionNote,
                item.CaptureMethod, item.ScannedAt, receipt.Status == "DONE" && item.SerialItemId.HasValue)).ToList()
        };
    }
}
