using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Services.Stocktaking;

internal static class StocktakeReceivingEvidence
{
    public static async Task<List<StocktakeSourceReceipt>> FindAsync(IStocktakeRepository repo, string serial,
        int warehouseId, string status, CancellationToken ct)
    {
        var inbound = await repo.Query<InboundItem>().AsNoTracking()
            .Where(i => i.IsActive && i.ScannedSerial == serial && i.Inbound.Status == status
                && i.Inbound.PurchaseOrder.WarehouseId == warehouseId)
            .Select(i => new StocktakeSourceReceipt("INBOUND", i.InboundId, i.Inbound.ReceiptCode, i.ProductId, i.Inbound.CompletedAt)).ToListAsync(ct);
        var returns = await repo.Query<ReturnReceiptItem>().AsNoTracking()
            .Where(i => i.IsActive && i.OutboundItem.SerialItem.SerialNumber == serial && i.ReturnReceipt.Status == status
                && i.ReturnReceipt.ReturnOrder.WarehouseId == warehouseId)
            .Select(i => new StocktakeSourceReceipt("RETURN", i.ReturnReceiptId, i.ReturnReceipt.ReceiptCode,
                i.OutboundItem.SerialItem.ProductId, i.ReturnReceipt.CompletedAt)).ToListAsync(ct);
        var transfers = await repo.Query<TransferReceiptItem>().AsNoTracking()
            .Where(i => i.IsActive && i.DispatchItem.SerialItem.SerialNumber == serial && i.TransferReceipt.Status == status
                && i.TransferReceipt.TransferOrder.ToWarehouseId == warehouseId)
            .Select(i => new StocktakeSourceReceipt("TRANSFER", i.TransferReceiptId, i.TransferReceipt.ReceiptCode,
                i.DispatchItem.ProductId, i.TransferReceipt.CompletedAt)).ToListAsync(ct);
        return inbound.Concat(returns).Concat(transfers).Distinct().ToList();
    }

    public static bool IsInWarehouse(SerialItem? item, int warehouseId) =>
        item is not null && item.StockState == "IN_WAREHOUSE" && item.CurrentWarehouseId == warehouseId;

    public static string BlockedMessage(SerialItem? item, IReadOnlyCollection<StocktakeSourceReceipt> receipts)
    {
        if (receipts.Count > 0)
            return "Serial có trên " + string.Join(", ", receipts.Select(r => r.Label))
                + " đang chờ hoàn tất. Hãy đối chiếu tem và xử lý theo chứng từ tiếp nhận.";
        return item is null
            ? "Chưa tìm thấy chứng từ theo serial. Kiểm tra lại tem hoặc báo Manager đối chiếu chứng từ giao nhận."
            : "Serial đã có trong hệ thống nhưng chưa ở tồn kho được giao. Kiểm tra chứng từ và trạng thái của hàng.";
    }
}
