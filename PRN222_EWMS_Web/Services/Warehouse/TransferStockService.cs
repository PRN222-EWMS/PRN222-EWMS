using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public sealed record TransferStockSource(long SourceId, int ProductId, long FreeQuantity);

public class TransferStockService(EwmsDbContext db)
{
    public async Task<List<TransferStockSource>> GetSourcesAsync(int warehouseId, string condition, long? excludeOrderId = null)
    {
        var sources = await db.SupplySources.AsNoTracking().Where(s => s.WarehouseId == warehouseId
            && s.ItemCondition == condition && s.SupplyState == "PHYSICAL").OrderBy(s => s.SupplySourceId).ToListAsync();
        var serials = await db.SerialItems.AsNoTracking().Where(s => s.CurrentWarehouseId == warehouseId
            && s.ItemCondition == condition && s.StockState == "IN_WAREHOUSE"
            && s.CurrentSupplyState == "PHYSICAL" && s.CurrentSupplySourceId != null)
            .Select(s => new { s.SerialItemId, SourceId = s.CurrentSupplySourceId!.Value, s.ProductId }).ToListAsync();
        var holds = await db.Reservations.AsNoTracking().Where(r => r.WarehouseId == warehouseId
            && r.ItemCondition == condition && r.SupplyState == "PHYSICAL" && r.Status == "ACTIVE"
            && (excludeOrderId == null || r.TransferOrderId != excludeOrderId))
            .Select(r => new { r.SupplySourceId, r.SalesOrderId, r.TransferOrderId,
                Qty = r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity }).ToListAsync();
        var claims = await db.PickingClaims.AsNoTracking().Where(c => c.SerialItem.CurrentWarehouseId == warehouseId
            && c.SerialItem.ItemCondition == condition)
            .Select(c => new { c.SerialItemId, SourceId = c.SerialItem.CurrentSupplySourceId,
                SalesOrderId = c.Outbound == null ? (long?)null : c.Outbound.SalesOrderId,
                TransferOrderId = c.Dispatch == null ? (long?)null : c.Dispatch.TransferOrderId }).ToListAsync();
        var validSerials = serials.Select(s => s.SerialItemId).ToHashSet();
        var result = new List<TransferStockSource>();
        foreach (var source in sources)
        {
            var sourceHolds = holds.Where(h => h.SupplySourceId == source.SupplySourceId).ToList();
            var sourceClaims = claims.Where(c => c.SourceId == source.SupplySourceId && validSerials.Contains(c.SerialItemId)).ToList();
            // Picking belongs inside the same demand's reservation, never subtract it twice.
            long covered = sourceClaims.GroupBy(c => (c.SalesOrderId, c.TransferOrderId)).Sum(g => Math.Min((long)g.Count(),
                sourceHolds.Where(h => h.SalesOrderId == g.Key.SalesOrderId && h.TransferOrderId == g.Key.TransferOrderId).Sum(h => (long)h.Qty)));
            var free = TransferPolicy.FreeQuantity(source.RemainingQuantity,
                serials.LongCount(s => s.SourceId == source.SupplySourceId && s.ProductId == source.ProductId),
                sourceHolds.Sum(h => (long)h.Qty), sourceClaims.Count, covered);
            result.Add(new(source.SupplySourceId, source.ProductId, free));
        }
        return result;
    }

    public async Task<List<ProductLookupDto>> GetProductsAsync(int warehouseId, string condition, long? excludeOrderId = null)
    {
        var stock = await GetSourcesAsync(warehouseId, condition, excludeOrderId);
        var products = await db.Products.AsNoTracking().Where(p => p.Status == "ACTIVE").OrderBy(p => p.ProductCode)
            .Select(p => new ProductLookupDto { ProductId = p.ProductId, ProductCode = p.ProductCode,
                ProductName = p.ProductName, Unit = p.Unit, CategoryId = p.CategoryId, BrandId = p.BrandId }).ToListAsync();
        foreach (var p in products) p.UnreservedQuantity = stock.Where(s => s.ProductId == p.ProductId).Sum(s => s.FreeQuantity);
        return products;
    }
}
