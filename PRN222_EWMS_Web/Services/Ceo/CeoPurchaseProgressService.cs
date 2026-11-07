using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Ceo;
using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Services.Ceo;

public class CeoPurchaseProgressService(ICeoPurchaseProgressRepository repository) : ICeoPurchaseProgressService
{
    private const int PageSize = 10;
    private static readonly string[] AllowedStatuses = ["DRAFT", "CONFIRMED", "PARTIALLY_RECEIVED", "RECEIVED", "CLOSED", "CANCELLED"];

    public async Task<CeoPurchaseProgressViewModel> GetPageAsync(CeoPurchaseProgressFilter filter)
    {
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        filter.Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim().ToUpperInvariant();
        if (filter.Search?.Length > 100 || filter.Status is not null && !AllowedStatuses.Contains(filter.Status)
            || filter.WarehouseId < 1 || filter.SupplierId < 1)
            throw new ArgumentException("Bộ lọc tiến độ đơn mua không hợp lệ.");

        var count = await repository.CountAsync(filter);
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        filter.Page = Math.Clamp(filter.Page, 1, pages);
        var orders = await repository.GetPageAsync(filter, filter.Page, PageSize);
        var progress = await repository.GetProgressAsync(orders.Select(po => po.PurchaseOrderId));
        var byOrder = progress.GroupBy(row => row.PurchaseOrderId).ToDictionary(group => group.Key, group => group.ToList());
        var rows = orders.Select(po =>
        {
            byOrder.TryGetValue(po.PurchaseOrderId, out var lines);
            var ordered = lines?.Sum(row => (long)row.OrderedQuantity) ?? po.PurchaseOrderLines.Sum(line => (long)line.OrderedQuantity);
            var received = lines?.Sum(row => row.ReceivedQuantity) ?? 0;
            var closed = lines?.Sum(row => (long)row.ClosedQuantity) ?? po.PurchaseOrderLines.Sum(line => (long)line.ClosedQuantity);
            var outstanding = Math.Max(0, ordered - received - closed);
            return new CeoPurchaseOrderRow(po.PurchaseOrderId, po.Pocode, po.Supplier.SupplierName,
                po.Warehouse.WarehouseName, po.Status, po.OrderDate, po.ExpectedArrivalDate,
                ordered, received, outstanding, closed);
        }).ToList();
        return new CeoPurchaseProgressViewModel
        {
            Filter = filter, TotalCount = count, TotalPages = pages, Orders = rows,
            Ordered = rows.Sum(row => row.Ordered), Received = rows.Sum(row => row.Received),
            Outstanding = rows.Sum(row => row.Outstanding), Closed = rows.Sum(row => row.Closed),
            Warehouses = await repository.GetWarehousesAsync(), Suppliers = await repository.GetSuppliersAsync()
        };
    }

    public async Task<CeoPurchaseOrderDetailViewModel?> GetDetailsAsync(long id)
    {
        var order = await repository.GetOrderAsync(id);
        if (order is null) return null;
        var progress = await repository.GetProgressAsync([id]);
        var byLine = progress.ToDictionary(row => row.PolineId);
        var lines = order.PurchaseOrderLines.OrderBy(line => line.Product.ProductCode).Select(line =>
        {
            byLine.TryGetValue(line.PolineId, out var row);
            var ordered = (long)line.OrderedQuantity;
            var received = row?.ReceivedQuantity ?? 0;
            var closed = (long)line.ClosedQuantity;
            return new CeoPurchaseLineRow(line.Product.ProductCode, line.Product.ProductName,
                ordered, received, Math.Max(0, ordered - received - closed), closed);
        }).ToList();
        return new CeoPurchaseOrderDetailViewModel
        {
            Id = id, Code = order.Pocode, Supplier = order.Supplier.SupplierName, Warehouse = order.Warehouse.WarehouseName,
            Status = order.Status, OrderDate = order.OrderDate, ExpectedArrivalDate = order.ExpectedArrivalDate,
            Lines = lines, Ordered = lines.Sum(line => line.Ordered), Received = lines.Sum(line => line.Received),
            Outstanding = lines.Sum(line => line.Outstanding), Closed = lines.Sum(line => line.Closed)
        };
    }
}
