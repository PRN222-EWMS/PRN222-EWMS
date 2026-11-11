using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;

public class PutawayRepository(EwmsDbContext db) : IPutawayRepository
{
    private static readonly string[] LockedStatuses = ["COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL"];

    public Task<PutawayActor?> GetActorAsync(int userId, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF"
            && u.WarehouseId != null && u.Warehouse != null)
        .Select(u => new PutawayActor(u.UserId, u.WarehouseId!.Value, u.Warehouse!.WarehouseName))
        .SingleOrDefaultAsync(ct);

    private IQueryable<SerialItem> Pending(int warehouseId) => db.SerialItems.AsNoTracking()
        .Where(s => s.CurrentWarehouseId == warehouseId && s.StockState == "IN_WAREHOUSE" && s.CurrentLocationId == null
            && s.CurrentSupplyState == "PHYSICAL"
            && s.CurrentSupplySource != null && s.CurrentSupplySource.WarehouseId == warehouseId
            && s.CurrentSupplySource.SupplyState == "PHYSICAL" && s.CurrentSupplySource.ProductId == s.ProductId
            && s.CurrentSupplySource.ItemCondition == s.ItemCondition
            && ((s.CurrentSupplySource.SourceType == "INBOUND" && s.CurrentSupplySource.Inbound != null
                    && s.CurrentSupplySource.Inbound.Status == "DONE"
                    && s.CurrentSupplySource.Inbound.PurchaseOrder.WarehouseId == warehouseId)
                || (s.CurrentSupplySource.SourceType == "RETURN" && s.CurrentSupplySource.ReturnReceipt != null
                    && s.CurrentSupplySource.ReturnReceipt.Status == "DONE"
                    && s.CurrentSupplySource.ReturnReceipt.ReturnOrder.WarehouseId == warehouseId)
                || (s.CurrentSupplySource.SourceType == "TRANSFER_RECEIPT" && s.CurrentSupplySource.TransferReceipt != null
                    && s.CurrentSupplySource.TransferReceipt.Status == "DONE"
                    && s.CurrentSupplySource.TransferReceipt.TransferOrder.ToWarehouseId == warehouseId)));

    public async Task<PutawayQueueViewModel> GetQueueAsync(int warehouseId, PutawayFilter filter, CancellationToken ct)
    {
        var sourceType = filter.Source switch
        {
            "PURCHASE" => "INBOUND",
            "RETURN" => "RETURN",
            "TRANSFER_RECEIPT" => "TRANSFER_RECEIPT",
            _ => throw new PutawayException("Nguồn hàng không hợp lệ.", 400)
        };
        var query = Pending(warehouseId).Where(s => s.CurrentSupplySource!.SourceType == sourceType)
            .Select(s => new
            {
                Id = s.SerialItemId, Serial = s.SerialNumber, s.Product.ProductCode, s.Product.ProductName,
                Category = s.Product.Category.CategoryName, Brand = s.Product.Brand.BrandName,
                Condition = s.ItemCondition, Quarantined = s.IsQuarantined,
                ReceiptId = sourceType == "RETURN" ? s.CurrentSupplySource!.ReturnReceiptId!.Value
                    : sourceType == "TRANSFER_RECEIPT" ? s.CurrentSupplySource!.TransferReceiptId!.Value
                    : s.CurrentSupplySource!.InboundId!.Value,
                ReceiptCode = sourceType == "RETURN" ? s.CurrentSupplySource!.ReturnReceipt!.ReceiptCode
                    : sourceType == "TRANSFER_RECEIPT" ? s.CurrentSupplySource!.TransferReceipt!.ReceiptCode
                    : s.CurrentSupplySource!.Inbound!.ReceiptCode,
                ReceivedAt = (sourceType == "RETURN" ? s.CurrentSupplySource!.ReturnReceipt!.CompletedAt
                    : sourceType == "TRANSFER_RECEIPT" ? s.CurrentSupplySource!.TransferReceipt!.CompletedAt
                    : s.CurrentSupplySource!.Inbound!.CompletedAt) ?? s.RegisteredAt,
                TransferOrderId = sourceType == "TRANSFER_RECEIPT" ? (long?)s.CurrentSupplySource!.TransferReceipt!.TransferOrderId : null,
                TransferCode = sourceType == "TRANSFER_RECEIPT" ? s.CurrentSupplySource!.TransferReceipt!.TransferOrder.TransferCode : null,
                FromWarehouseName = sourceType == "TRANSFER_RECEIPT"
                    ? s.CurrentSupplySource!.TransferReceipt!.TransferOrder.FromWarehouse.WarehouseName : null
            });
        var receiptRows = await query.GroupBy(s => new
            {
                Id = s.ReceiptId, Code = s.ReceiptCode, s.TransferCode, s.FromWarehouseName
            })
            .Select(g => new { g.Key.Id, g.Key.Code, PendingCount = g.Count(), g.Key.TransferCode, g.Key.FromWarehouseName })
            .OrderByDescending(r => r.Id).ToListAsync(ct);
        var receipts = receiptRows.Select(r => new PutawayReceiptOption(r.Id, r.Code, r.PendingCount, r.TransferCode, r.FromWarehouseName)).ToList();

        if (filter.ReceiptId is long receiptId)
        {
            if (receipts.All(r => r.Id != receiptId))
            {
                // Retain an explicitly selected, now-empty DONE receipt when another operator finishes it.
                var receipt = sourceType switch
                {
                    "RETURN" => await db.ReturnReceipts.AsNoTracking().Where(r => r.ReturnReceiptId == receiptId
                        && r.ReturnOrder.WarehouseId == warehouseId && r.Status == "DONE")
                        .Select(r => new PutawayReceiptOption(r.ReturnReceiptId, r.ReceiptCode, 0, null, null)).SingleOrDefaultAsync(ct),
                    "TRANSFER_RECEIPT" => await db.TransferReceipts.AsNoTracking().Where(r => r.TransferReceiptId == receiptId
                        && r.TransferOrder.ToWarehouseId == warehouseId && r.Status == "DONE")
                        .Select(r => new PutawayReceiptOption(r.TransferReceiptId, r.ReceiptCode, 0,
                            r.TransferOrder.TransferCode, r.TransferOrder.FromWarehouse.WarehouseName)).SingleOrDefaultAsync(ct),
                    _ => await db.Inbounds.AsNoTracking().Where(r => r.InboundId == receiptId
                        && r.PurchaseOrder.WarehouseId == warehouseId && r.Status == "DONE")
                        .Select(r => new PutawayReceiptOption(r.InboundId, r.ReceiptCode, 0, null, null)).SingleOrDefaultAsync(ct)
                };
                if (receipt is null) throw new PutawayException("Phiếu đã chọn không thuộc nguồn hàng hoặc kho được giao, hoặc chưa hoàn tất.", 404);
                receipts.Insert(0, receipt);
            }
            query = query.Where(s => s.ReceiptId == receiptId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(s => s.Serial.Contains(search) || s.ProductCode.Contains(search)
                || s.ProductName.Contains(search) || s.ReceiptCode.Contains(search)
                || (s.TransferCode != null && s.TransferCode.Contains(search))
                || (s.FromWarehouseName != null && s.FromWarehouseName.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Condition)) query = query.Where(s => s.Condition == filter.Condition);
        var count = await query.CountAsync(ct);
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, (count + PutawayQueueViewModel.PageSize - 1) / PutawayQueueViewModel.PageSize));
        var items = await query.OrderBy(s => s.ReceivedAt).ThenBy(s => s.Id)
            .Skip((filter.Page - 1) * PutawayQueueViewModel.PageSize).Take(PutawayQueueViewModel.PageSize)
            .Select(s => new PutawayPendingItem(s.Id, s.Serial, s.ProductCode, s.ProductName,
                s.Category, s.Brand, s.Condition, s.Quarantined, s.ReceiptId, s.ReceiptCode, s.ReceivedAt,
                s.TransferOrderId, s.TransferCode, s.FromWarehouseName))
            .ToListAsync(ct);
        return new PutawayQueueViewModel
        {
            Filter = filter, Receipts = receipts, TotalCount = count,
            Items = items.Select(i => i with { ReceivedAt = DateTime.SpecifyKind(i.ReceivedAt, DateTimeKind.Utc) }).ToList()
        };
    }

    public Task<SerialItem?> GetSerialAsync(int warehouseId, long? id, string? serial, bool tracked, CancellationToken ct)
    {
        var query = db.SerialItems.Where(s => s.CurrentWarehouseId == warehouseId
            && (id.HasValue ? s.SerialItemId == id.Value : s.SerialNumber == serial));
        if (!tracked) query = query.AsNoTracking();
        return query.Include(s => s.Product).ThenInclude(p => p.Category)
            .Include(s => s.Product).ThenInclude(p => p.Brand)
            .Include(s => s.CurrentSupplySource).ThenInclude(s => s!.Inbound).ThenInclude(i => i!.PurchaseOrder)
            .Include(s => s.CurrentSupplySource).ThenInclude(s => s!.ReturnReceipt).ThenInclude(r => r!.ReturnOrder)
            .Include(s => s.CurrentSupplySource).ThenInclude(s => s!.TransferReceipt).ThenInclude(r => r!.TransferOrder).ThenInclude(o => o.FromWarehouse)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
    }

    public async Task<PutawayLayout?> GetLayoutAsync(int warehouseId, CancellationToken ct)
    {
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);
        if (warehouse is null) return null;
        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);
        var rules = await db.ZoneStorageRules.AsNoTracking().Where(r => r.ZoneLocation.WarehouseId == warehouseId).ToListAsync(ct);
        var stocktakes = await db.Stocktakes.AsNoTracking().Where(s => s.WarehouseId == warehouseId && LockedStatuses.Contains(s.Status))
            .Select(s => new { s.StocktakeId, s.ScopeType, s.IncludeAwaitingPutaway }).ToListAsync(ct);
        var ids = stocktakes.Select(s => s.StocktakeId).ToArray();
        var scopes = await db.StocktakeScopes.AsNoTracking().Where(s => ids.Contains(s.StocktakeId))
            .Select(s => new { s.StocktakeId, s.WarehouseId, s.LocationId }).ToListAsync(ct);
        var awaitingLocked = stocktakes.Any(s => s.ScopeType == "WHOLE_WAREHOUSE" || s.IncludeAwaitingPutaway)
            || stocktakes.Any(s => s.ScopeType != "LOCATIONS" && s.ScopeType != "WHOLE_WAREHOUSE")
            || stocktakes.Any(s => s.ScopeType == "LOCATIONS" && scopes.All(x => x.StocktakeId != s.StocktakeId))
            || scopes.Any(s => s.WarehouseId != warehouseId || locations.All(l => l.LocationId != s.LocationId));
        return new PutawayLayout(warehouse, locations, rules, awaitingLocked, scopes.Select(s => s.LocationId).ToHashSet());
    }

    public Task<bool> HasPickingClaimAsync(long serialId, CancellationToken ct) =>
        db.PickingClaims.AsNoTracking().AnyAsync(c => c.SerialItemId == serialId, ct);

    public Task<List<PutawayOccupant>> GetOccupantsAsync(int warehouseId, int? locationId, CancellationToken ct) =>
        db.SerialItems.AsNoTracking().Where(s => s.StockState == "IN_WAREHOUSE" && s.CurrentLocationId != null
            && s.CurrentLocation!.WarehouseId == warehouseId && (!locationId.HasValue || s.CurrentLocationId == locationId.Value))
            .Select(s => new PutawayOccupant(s.SerialItemId, s.CurrentWarehouseId ?? 0, s.CurrentLocationId!.Value, s.ItemCondition,
                s.Product.LengthCm, s.Product.WidthCm, s.Product.HeightCm, s.Product.GrossWeightKg)).ToListAsync(ct);

    public async Task<T> InTransactionAsync<T>(int userId, int warehouseId, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Same first lock as warehouse configuration: an empty-branch edit and putaway cannot pass stale guards.
            await db.Database.SqlQuery<int>($"SELECT WarehouseID AS [Value] FROM dbo.Warehouse WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}").ToListAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={userId}", ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    public Task LockSerialAsync(int warehouseId, long serialId, CancellationToken ct) => db.Database.SqlQuery<long>(
        $"SELECT SerialItemID AS [Value] FROM dbo.SerialItem WITH (UPDLOCK,HOLDLOCK) WHERE SerialItemID={serialId} AND CurrentWarehouseID={warehouseId}").ToListAsync(ct);

    public async Task SavePlacementAsync(SerialItem serial, int locationId, int userId, byte[] version, string path, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Putaway requires a transaction.");
        db.Entry(serial).Property(s => s.RowVersion).OriginalValue = version;
        serial.CurrentLocationId = locationId;
        db.SerialMovements.Add(new SerialMovement
        {
            SerialItemId = serial.SerialItemId, PerformedByUserId = userId, EventType = "PUTAWAY", OccurredAt = DateTime.UtcNow,
            FromState = serial.StockState, ToState = serial.StockState,
            FromCondition = serial.ItemCondition, ToCondition = serial.ItemCondition,
            FromWarehouseId = serial.CurrentWarehouseId, ToWarehouseId = serial.CurrentWarehouseId,
            FromLocationId = null, ToLocationId = locationId,
            FromQuarantined = serial.IsQuarantined, ToQuarantined = serial.IsQuarantined,
            Note = $"Putaway: {path}"
        });
        // Existing audit triggers use ActorUserID; no second serial/source/receipt is created.
        await db.SaveChangesAsync(ct);
    }
}
