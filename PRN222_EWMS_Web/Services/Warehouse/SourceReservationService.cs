using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Services.Warehouse;

/// <summary>Quantity commitments shared by sales, receiving and stock adjustments; never selects serials.</summary>
public class SourceReservationService(EwmsDbContext db, TimeProvider clock) : ISourceReservationService
{
    private static readonly string[] OpenStatuses = ["SUBMITTED", "PARTIALLY_FULFILLED"];
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public static long Balance(Reservation r) => Math.Max(0, (long)r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity);

    public async Task LockWarehouseAsync(int warehouseId, CancellationToken ct = default)
    {
        RequireTransaction();
        // Match the existing Transfer and Stocktake warehouse lock order.
        await db.Warehouses.FromSqlInterpolated($"SELECT * FROM dbo.Warehouse WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}")
            .AsNoTracking().Select(w => w.WarehouseId).ToListAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={"EWMS:WarehouseSupply:" + warehouseId},
                 @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @result<0 THROW 51260,N'Không lấy được quyền cập nhật nguồn hàng. Vui lòng thử lại.',1;", ct);
        await db.SupplySources.FromSqlInterpolated($"SELECT * FROM dbo.SupplySource WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}")
            .AsNoTracking().Select(s => s.SupplySourceId).ToListAsync(ct);
        await db.Reservations.FromSqlInterpolated($"SELECT * FROM dbo.Reservation WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}")
            .AsNoTracking().Select(r => r.ReservationId).ToListAsync(ct);
    }

    // The caller holds the warehouse lock in its posting transaction.
    public async Task<string?> GetReceivingBlockReasonAsync(int warehouseId, CancellationToken ct = default)
    {
        RequireTransaction();
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);
        return ReceivingWarehouseError(warehouse, Today)
            ?? await GetStocktakeBlockReasonAsync(warehouseId, [null], ct);
    }

    public static string? ReceivingWarehouseError(Models.Warehouse? warehouse, DateOnly today)
    {
        if (warehouse?.Status != "ACTIVE") return "Kho nhận không tồn tại hoặc đã ngừng hoạt động.";
        if (warehouse.WarehouseType == "RENTED" && (warehouse.RentalStartDate is not DateOnly start
            || warehouse.RentalEndDate is not DateOnly end || today < start || today > end))
            return "Kho nhận chưa bắt đầu thuê, đã hết hạn hoặc thiếu kỳ thuê hợp lệ. Cần gia hạn trước khi nhận hàng.";
        return null;
    }

    public async Task<string?> GetStocktakeBlockReasonAsync(int warehouseId, IReadOnlyCollection<int?> locationIds, CancellationToken ct = default)
    {
        RequireTransaction();
        if (locationIds.Count == 0) return null;
        var plans = await db.Stocktakes.AsNoTracking().Where(s => s.WarehouseId == warehouseId
            && StocktakeScopeRules.LockedStatuses.Contains(s.Status)).ToListAsync(ct);
        if (plans.Count == 0) return null;
        var planIds = plans.Select(s => s.StocktakeId).ToArray();
        var scopes = await db.StocktakeScopes.AsNoTracking().Where(s => planIds.Contains(s.StocktakeId)).ToListAsync(ct);
        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);
        return StocktakeScopeRules.MovementBlockReason(warehouseId, plans, scopes, locations, locationIds);
    }

    private IQueryable<SupplySource> Graph(bool tracked = false)
    {
        var query = tracked ? db.SupplySources.AsQueryable() : db.SupplySources.AsNoTracking();
        return query.Include(s => s.Product).Include(s => s.Poline).ThenInclude(l => l!.PurchaseOrder)
            .ThenInclude(p => p.Supplier)
            .Include(s => s.Inbound).Include(s => s.ReturnReceipt).Include(s => s.TransferReceipt)
            .Include(s => s.Adjustment).Include(s => s.IncomingTransferLine).ThenInclude(l => l!.TransferOrder)
            .ThenInclude(o => o.FromWarehouse).AsSplitQuery();
    }

    public static bool IsPhysical(SupplySource s) => s.ItemCondition == "GOOD" && IsPostedPhysical(s);

    public static bool IsPostedPhysical(SupplySource s) => s.SupplyState == "PHYSICAL" && (s.SourceType switch
    {
        "INBOUND" => s.Inbound?.Status == "DONE",
        "RETURN" => s.ReturnReceipt?.Status == "DONE",
        "TRANSFER_RECEIPT" => s.TransferReceipt?.Status == "DONE",
        "ADJUSTMENT" => s.Adjustment?.Status == "APPLIED",
        _ => false
    });

    public static bool IsIncoming(SupplySource s) => s.SupplyState == "INCOMING" && s.ItemCondition == "GOOD" && (s.SourceType switch
    {
        "PO_INCOMING" => s.Poline?.PurchaseOrder.Status is "CONFIRMED" or "PARTIALLY_RECEIVED",
        "TRANSFER_INCOMING" => s.IncomingTransferLine?.TransferOrder.Status is "RELEASED" or "IN_PROGRESS",
        _ => false
    });

    public static string Label(SupplySource s) => s.SourceType switch
    {
        "PO_INCOMING" => $"Chờ PO {s.Poline?.PurchaseOrder.Pocode} · {s.Poline?.PurchaseOrder.Supplier?.SupplierName}",
        "TRANSFER_INCOMING" => $"Chờ chuyển {s.IncomingTransferLine?.TransferOrder.TransferCode} · {s.IncomingTransferLine?.TransferOrder.FromWarehouse.WarehouseName}",
        "INBOUND" => $"Đã nhận · {s.Inbound?.ReceiptCode}",
        "TRANSFER_RECEIPT" => $"Đã nhận chuyển · {s.TransferReceipt?.ReceiptCode}",
        "RETURN" => $"Đã nhận trả · {s.ReturnReceipt?.ReceiptCode}",
        "ADJUSTMENT" => $"Điều chỉnh · {s.Adjustment?.AdjustmentCode}",
        _ => $"Nguồn #{s.SupplySourceId}"
    };

    public async Task<List<SalesSourceOption>> GetOptionsAsync(int warehouseId, DateOnly? requestedDate, long? orderId = null, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);
        if (warehouse?.Status != "ACTIVE") return [];
        var sources = await Graph().Where(s => s.WarehouseId == warehouseId && s.ItemCondition == "GOOD" && s.Product.Status == "ACTIVE").ToListAsync(ct);
        var holds = await db.Reservations.AsNoTracking().Where(r => r.WarehouseId == warehouseId && r.Status == "ACTIVE").ToListAsync(ct);
        var serialCounts = await db.SerialItems.AsNoTracking().Where(s => s.CurrentWarehouseId == warehouseId && s.StockState == "IN_WAREHOUSE"
                && s.CurrentSupplyState == "PHYSICAL" && s.ItemCondition == "GOOD" && s.CurrentSupplySourceId != null)
            .GroupBy(s => s.CurrentSupplySourceId!.Value).Select(g => new { Id = g.Key, Count = g.LongCount() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var claims = await db.PickingClaims.AsNoTracking().Where(c => c.SerialItem.CurrentWarehouseId == warehouseId && c.SerialItem.CurrentSupplySourceId != null)
            .Select(c => new Claim(c.SerialItem.CurrentSupplySourceId!.Value,
                c.Outbound == null ? null : (long?)c.Outbound.SalesOrderId,
                c.Dispatch == null ? null : (long?)c.Dispatch.TransferOrderId)).ToListAsync(ct);
        var transit = await db.VwTransferInTransits.AsNoTracking().Where(t => t.ToWarehouseId == warehouseId && t.DispatchedCondition == "GOOD")
            .GroupBy(t => t.TransferLineId).Select(g => new { Id = g.Key, Count = g.LongCount() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var result = new List<SalesSourceOption>();
        foreach (var s in sources)
        {
            bool physical = IsPhysical(s), incoming = IsIncoming(s);
            if (!physical && !incoming) continue;
            long capacity = Math.Max(0, Math.Min((long)s.RemainingQuantity, s.EligibleQuantity));
            if (physical) capacity = Math.Min(s.RemainingQuantity, serialCounts.GetValueOrDefault(s.SupplySourceId));
            if (s.TransferLineId is long transferLine) capacity = Math.Min(capacity, transit.GetValueOrDefault(transferLine));
            var sourceHolds = holds.Where(r => r.SupplySourceId == s.SupplySourceId).ToList();
            var sourceClaims = claims.Where(c => c.SourceId == s.SupplySourceId).ToList();
            var own = orderId.HasValue ? sourceHolds.Where(r => r.SalesOrderId == orderId).Sum(Balance) : 0;
            var covered = sourceClaims.GroupBy(c => (c.SalesOrderId, c.TransferOrderId)).Sum(g => Math.Min((long)g.Count(),
                sourceHolds.Where(r => r.SalesOrderId == g.Key.SalesOrderId && r.TransferOrderId == g.Key.TransferOrderId).Sum(Balance)));
            var free = Math.Max(0, capacity - sourceHolds.Sum(Balance) - Math.Max(0, sourceClaims.Count - covered));
            string? warning = physical ? null : ArrivalWarning(s.AvailableFrom, requestedDate, warehouse);
            result.Add(new SalesSourceOption
            {
                SupplySourceId = s.SupplySourceId, TransferLineId = s.TransferLineId, ProductId = s.ProductId,
                SourceType = s.SourceType, SupplyState = s.SupplyState, Label = Label(s), AvailableFrom = s.AvailableFrom,
                AvailableQuantity = free + own, HeldForOrder = own, CanSelect = warning == null && free + own > 0,
                Warning = warning ?? (incoming && s.AvailableFrom == requestedDate ? "Hàng đến đúng ngày phải xuất, không có thời gian dự phòng." : null)
            });
        }
        // Read-only preview for pre-existing shipments. Persist a forecast row only in a POST transaction.
        var virtualIds = transit.Keys.Except(sources.Where(s => s.TransferLineId.HasValue).Select(s => s.TransferLineId!.Value)).ToArray();
        var virtualLines = await db.TransferOrderLines.AsNoTracking().Where(l => virtualIds.Contains(l.TransferLineId)
                && l.Product.Status == "ACTIVE" && l.TransferOrder.ItemCondition == "GOOD"
                && (l.TransferOrder.Status == "RELEASED" || l.TransferOrder.Status == "IN_PROGRESS"))
            .Include(l => l.TransferOrder).ThenInclude(o => o.FromWarehouse).ToListAsync(ct);
        foreach (var line in virtualLines)
        {
            var warning = ArrivalWarning(line.TransferOrder.ExpectedDate, requestedDate, warehouse);
            result.Add(new SalesSourceOption
            {
                TransferLineId = line.TransferLineId, ProductId = line.ProductId, SourceType = "TRANSFER_INCOMING", SupplyState = "INCOMING",
                Label = $"Chờ chuyển {line.TransferOrder.TransferCode} · {line.TransferOrder.FromWarehouse.WarehouseName}",
                AvailableFrom = line.TransferOrder.ExpectedDate, AvailableQuantity = transit[line.TransferLineId], CanSelect = warning == null, Warning = warning
            });
        }
        return result.OrderBy(s => s.ProductId).ThenBy(s => s.AvailableFrom).ThenBy(s => s.SupplySourceId).ToList();
    }

    private string? ArrivalWarning(DateOnly? arrival, DateOnly? deadline, Models.Warehouse warehouse)
    {
        if (!deadline.HasValue) return "Chọn ngày phải xuất để kiểm nguồn dự kiến.";
        if (!arrival.HasValue) return "Nguồn chưa có ngày dự kiến đến.";
        if (arrival < Today) return "Nguồn đã quá ngày dự kiến nhưng chưa nhận; cần xác nhận lại lịch trước khi giữ mới.";
        if (arrival > deadline) return "Nguồn về sau ngày phải xuất.";
        if (warehouse.WarehouseType == "RENTED" && (warehouse.RentalStartDate is not DateOnly start
            || warehouse.RentalEndDate is not DateOnly end || arrival < start || arrival > end))
            return "Ngày hàng đến ngoài thời hạn thuê kho.";
        return null;
    }

    public async Task<long> ResolveSourceAsync(int warehouseId, SalesSourceChoiceInput choice, CancellationToken ct = default)
    {
        RequireTransaction();
        if (choice.SupplySourceId > 0) return choice.SupplySourceId;
        if (choice.TransferLineId is not long lineId) throw new InvalidOperationException("Vui lòng chọn nguồn hàng.");
        var source = await EnsureTransitSourceAsync(warehouseId, lineId, ct);
        return source?.SupplySourceId ?? throw new InvalidOperationException("Nguồn chuyển kho không còn hàng GOOD đang đi tới kho này.");
    }

    private async Task<SupplySource?> EnsureTransitSourceAsync(int warehouseId, long lineId, CancellationToken ct)
    {
        var line = await db.TransferOrderLines.AsNoTracking().Include(l => l.TransferOrder).SingleOrDefaultAsync(l => l.TransferLineId == lineId, ct);
        if (line == null || line.TransferOrder.ToWarehouseId != warehouseId || line.TransferOrder.ItemCondition != "GOOD") return null;
        var quantity = await db.VwTransferInTransits.LongCountAsync(t => t.TransferLineId == lineId && t.ToWarehouseId == warehouseId && t.DispatchedCondition == "GOOD", ct);
        if (line.TransferOrder.Status is not ("RELEASED" or "IN_PROGRESS")) quantity = 0;
        var source = await db.SupplySources.SingleOrDefaultAsync(s => s.TransferLineId == lineId, ct);
        if (source == null)
        {
            if (quantity == 0 || line.TransferOrder.ExpectedDate == null) return null;
            source = new SupplySource
            {
                ProductId = line.ProductId, WarehouseId = warehouseId, ItemCondition = "GOOD", SourceType = "TRANSFER_INCOMING",
                SupplyState = "INCOMING", TransferLineId = lineId, AvailableFrom = line.TransferOrder.ExpectedDate, CreatedAt = Now
            };
            db.SupplySources.Add(source);
        }
        source.RemainingQuantity = source.EligibleQuantity = checked((int)quantity);
        await db.SaveChangesAsync(ct);
        return source;
    }

    public async Task ReservePlansAsync(SalesOrder order, CancellationToken ct = default)
    {
        RequireTransaction();
        if (order.Status != "DRAFT") throw new InvalidOperationException("Chỉ được phát hành SO Draft.");
        var options = (await GetOptionsAsync(order.WarehouseId, order.RequestedDate, order.SalesOrderId, ct))
            .Where(s => s.SupplySourceId > 0).ToDictionary(s => s.SupplySourceId);
        foreach (var line in order.SalesOrderLines)
        {
            var plans = await db.SalesOrderSourcePlans.Where(p => p.SolineId == line.SolineId).ToListAsync(ct);
            if (plans.Sum(p => (long)p.Quantity) != line.RequestedQuantity)
                throw new InvalidOperationException($"Dòng sản phẩm #{line.ProductId}: tổng nguồn phải bằng {line.RequestedQuantity}.");
            foreach (var plan in plans)
            {
                if (!options.TryGetValue(plan.SupplySourceId, out var option) || option.ProductId != line.ProductId
                    || !option.CanSelect || plan.Quantity > option.AvailableQuantity)
                    throw new InvalidOperationException($"Nguồn của sản phẩm #{line.ProductId} đã thay đổi hoặc không còn đủ/kịp ngày. Vui lòng kiểm tra lại.");
                db.Reservations.Add(CreateReservation(line, option.SupplySourceId, option.SupplyState, plan.Quantity));
                option.AvailableQuantity -= plan.Quantity;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private Reservation CreateReservation(SalesOrderLine line, long sourceId, string supplyState, int quantity, long? replaces = null) => new()
    {
        SupplySourceId = sourceId, SalesOrderId = line.SalesOrderId, SolineId = line.SolineId,
        ProductId = line.ProductId, WarehouseId = line.SalesOrder.WarehouseId, ItemCondition = "GOOD", SupplyState = supplyState,
        ReservedQuantity = quantity, Status = "ACTIVE", CreatedAt = Now, ReplacesReservationId = replaces
    };

    public async Task AfterDispatchAsync(long transferOrderId, CancellationToken ct = default)
    {
        RequireTransaction();
        var order = await db.TransferOrders.AsNoTracking().SingleAsync(o => o.TransferOrderId == transferOrderId, ct);
        if (order.ItemCondition != "GOOD") return;
        await LockWarehouseAsync(order.ToWarehouseId, ct);
        foreach (var id in await db.TransferOrderLines.Where(l => l.TransferOrderId == transferOrderId).Select(l => l.TransferLineId).ToListAsync(ct))
            await EnsureTransitSourceAsync(order.ToWarehouseId, id, ct);
    }

    public async Task AfterInboundAsync(long inboundId, CancellationToken ct = default)
    {
        RequireTransaction();
        var receipt = await db.Inbounds.AsNoTracking().Include(i => i.PurchaseOrder).SingleAsync(i => i.InboundId == inboundId, ct);
        if (receipt.Status != "DONE") throw new InvalidOperationException("Phiếu nhập chưa hoàn tất.");
        await LockWarehouseAsync(receipt.PurchaseOrder.WarehouseId, ct);
        var received = await db.InboundItems.AsNoTracking().Where(i => i.InboundId == inboundId && i.IsActive && i.ReceivedCondition == "GOOD")
            .GroupBy(i => new { i.PolineId, i.ProductId }).Select(g => new { g.Key.PolineId, g.Key.ProductId, Quantity = g.Count() }).ToListAsync(ct);
        foreach (var group in received)
        {
            var incoming = await db.SupplySources.SingleOrDefaultAsync(s => s.PolineId == group.PolineId, ct);
            var physical = await db.SupplySources.SingleAsync(s => s.InboundId == inboundId && s.ProductId == group.ProductId && s.ItemCondition == "GOOD", ct);
            if (incoming != null) await MigrateAsync(incoming, physical, group.Quantity, ct);
        }
        await ReconcileWarehouseAsync(receipt.PurchaseOrder.WarehouseId, "Hàng PO đã nhận: cập nhật cam kết theo lượng GOOD thực tế.", ct);
    }

    public async Task AfterTransferReceiptAsync(long receiptId, CancellationToken ct = default)
    {
        RequireTransaction();
        var receipt = await db.TransferReceipts.AsNoTracking().Include(r => r.TransferOrder).SingleAsync(r => r.TransferReceiptId == receiptId, ct);
        if (receipt.Status != "DONE") throw new InvalidOperationException("Phiếu nhận chuyển chưa hoàn tất.");
        var warehouseId = receipt.TransferOrder.ToWarehouseId;
        await LockWarehouseAsync(warehouseId, ct);
        var received = await db.TransferReceiptItems.AsNoTracking().Where(i => i.TransferReceiptId == receiptId && i.IsActive && i.ReceivedCondition == "GOOD")
            .GroupBy(i => new { i.DispatchItem.TransferLineId, i.DispatchItem.ProductId })
            .Select(g => new { g.Key.TransferLineId, g.Key.ProductId, Quantity = g.Count() }).ToListAsync(ct);
        foreach (var group in received)
        {
            var incoming = await db.SupplySources.SingleOrDefaultAsync(s => s.TransferLineId == group.TransferLineId, ct);
            var physical = await db.SupplySources.SingleAsync(s => s.TransferReceiptId == receiptId && s.ProductId == group.ProductId && s.ItemCondition == "GOOD", ct);
            if (incoming != null) await MigrateAsync(incoming, physical, group.Quantity, ct);
        }
        await ReconcileWarehouseAsync(warehouseId, "Hàng chuyển kho đã nhận: cập nhật cam kết theo tình trạng thực tế.", ct);
    }

    // Integration hook for the separate loss-approval workflow. Caller owns the Apply transaction.
    public async Task AfterTransferLossAppliedAsync(long lossRequestId, CancellationToken ct = default)
    {
        RequireTransaction();
        var loss = await db.TransferLossRequests.AsNoTracking().Include(l => l.TransferOrder)
            .SingleOrDefaultAsync(l => l.LossRequestId == lossRequestId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy đề nghị mất hàng Transfer.");
        if (loss.AppliedAt == null) throw new InvalidOperationException("Chỉ đối soát mất hàng sau khi quyết định đã được áp dụng.");
        await ReconcileWarehouseAsync(loss.TransferOrder.ToWarehouseId,
            $"Mất hàng của Transfer {loss.TransferOrder.TransferCode} đã được áp dụng; lượng đang đi giảm theo chứng từ.", ct);
    }

    private async Task MigrateAsync(SupplySource incoming, SupplySource physical, int goodReceived, CancellationToken ct)
    {
        var previous = await db.Reservations.Where(r => r.SupplySourceId == physical.SupplySourceId && r.ReplacesReservation != null
            && r.ReplacesReservation.SupplySourceId == incoming.SupplySourceId).SumAsync(r => (long?)r.ReservedQuantity, ct) ?? 0;
        var remaining = Math.Max(0, goodReceived - previous);
        var reservations = await db.Reservations.Where(r => r.SupplySourceId == incoming.SupplySourceId && r.Status == "ACTIVE" && r.SolineId != null
                && r.SalesOrder != null && OpenStatuses.Contains(r.SalesOrder.Status))
            .Include(r => r.SalesOrder)
            .Include(r => r.SalesOrderLine).ThenInclude(l => l!.SalesOrder)
            .OrderBy(r => r.SalesOrder!.RequestedDate).ThenBy(r => r.SalesOrder!.SubmittedAt).ThenBy(r => r.ReservationId).ToListAsync(ct);
        foreach (var old in reservations)
        {
            if (remaining == 0) break;
            var take = checked((int)Math.Min(remaining, Balance(old)));
            if (take == 0) continue;
            Release(old, take);
            db.Reservations.Add(CreateReservation(old.SalesOrderLine!, physical.SupplySourceId, "PHYSICAL", take, old.ReservationId));
            remaining -= take;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ReconcileWarehouseAsync(int warehouseId, string reason, CancellationToken ct = default)
    {
        RequireTransaction();
        await LockWarehouseAsync(warehouseId, ct);
        foreach (var lineId in await db.SupplySources.Where(s => s.WarehouseId == warehouseId && s.TransferLineId != null).Select(s => s.TransferLineId!.Value).ToListAsync(ct))
            await EnsureTransitSourceAsync(warehouseId, lineId, ct);
        var sources = await Graph(true).Where(s => s.WarehouseId == warehouseId).ToListAsync(ct);
        var holds = await db.Reservations.Where(r => r.WarehouseId == warehouseId && r.Status == "ACTIVE")
            .Include(r => r.SalesOrder).Include(r => r.TransferOrder).ToListAsync(ct);
        var affected = new HashSet<long>();
        var physicalCounts = await db.SerialItems.AsNoTracking().Where(s => s.CurrentWarehouseId == warehouseId
                && s.StockState == "IN_WAREHOUSE" && s.CurrentSupplyState == "PHYSICAL" && s.CurrentSupplySourceId != null)
            .GroupBy(s => new { SourceId = s.CurrentSupplySourceId!.Value, s.ProductId, s.ItemCondition })
            .Select(g => new { g.Key.SourceId, g.Key.ProductId, g.Key.ItemCondition, Count = g.Count() }).ToListAsync(ct);
        foreach (var source in sources)
        {
            if (IsPostedPhysical(source))
            {
                var count = physicalCounts.SingleOrDefault(c => c.SourceId == source.SupplySourceId
                    && c.ProductId == source.ProductId && c.ItemCondition == source.ItemCondition)?.Count ?? 0;
                source.RemainingQuantity = source.EligibleQuantity = count;
            }
            var sourceHolds = holds.Where(r => r.SupplySourceId == source.SupplySourceId && Balance(r) > 0).ToList();
            foreach (var r in sourceHolds.Where(r => r.SalesOrderId.HasValue && (r.SalesOrder == null || !OpenStatuses.Contains(r.SalesOrder.Status)
                || r.SupplyState != source.SupplyState || (source.SupplyState == "INCOMING" && (!IsIncoming(source)
                    || !source.AvailableFrom.HasValue || !r.SalesOrder.RequestedDate.HasValue || source.AvailableFrom > r.SalesOrder.RequestedDate)))))
            {
                Release(r, Balance(r));
                affected.Add(r.SalesOrderId!.Value);
                if (r.SalesOrder != null && OpenStatuses.Contains(r.SalesOrder.Status)) r.SalesOrder.HasShortage = true;
            }
            long capacity = IsPostedPhysical(source) || IsIncoming(source)
                ? Math.Max(0, Math.Min((long)source.RemainingQuantity, source.EligibleQuantity)) : 0;
            var excess = Math.Max(0, sourceHolds.Sum(Balance) - capacity);
            if (excess > 0 && await db.PickingClaims.AnyAsync(c => c.SerialItem.CurrentSupplySourceId == source.SupplySourceId, ct))
                throw new InvalidOperationException("Nguồn bị giảm đang có serial được chọn; cần xử lý phiên lấy hàng trước.");
            // Physical sources may also serve transfers: use their dispatch deadline instead of treating them as undated SOs.
            foreach (var r in sourceHolds.OrderByDescending(r => r.SalesOrder?.RequestedDate ?? r.TransferOrder?.DispatchDate ?? DateOnly.MaxValue)
                         .ThenByDescending(r => r.SalesOrder?.SubmittedAt ?? r.CreatedAt).ThenByDescending(r => r.ReservationId))
            {
                if (excess == 0) break;
                var release = Math.Min(excess, Balance(r));
                Release(r, release); excess -= release;
                if (release > 0 && r.SalesOrderId.HasValue)
                {
                    affected.Add(r.SalesOrderId.Value);
                    if (r.SalesOrder != null && OpenStatuses.Contains(r.SalesOrder.Status)) r.SalesOrder.HasShortage = true;
                }
            }
        }
        await db.SaveChangesAsync(ct);
        var orders = await db.SalesOrders.Where(o => o.WarehouseId == warehouseId && OpenStatuses.Contains(o.Status)
                && o.SalesOrderLines.Any(l => l.SourcePlans.Any()))
            .Include(o => o.SalesOrderLines).ToListAsync(ct);
        var balances = await db.Reservations.AsNoTracking().Where(r => r.WarehouseId == warehouseId && r.Status == "ACTIVE" && r.SolineId != null)
            .GroupBy(r => r.SolineId!.Value).Select(g => new { Id = g.Key, Quantity = g.Sum(r => (long)r.ReservedQuantity-r.ConsumedQuantity-r.ReleasedQuantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var issued = await db.OutboundItems.AsNoTracking().Where(i => i.IsActive && i.Outbound.Status == "DONE" && i.WarehouseId == warehouseId)
            .GroupBy(i => i.SolineId).Select(g => new { Id = g.Key, Quantity = g.LongCount() }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        foreach (var order in orders)
        {
            var shortage = order.SalesOrderLines.Any(l => Math.Max(0, (long)l.RequestedQuantity-l.CancelledQuantity-issued.GetValueOrDefault(l.SolineId)) > balances.GetValueOrDefault(l.SolineId));
            if (shortage && (!order.HasShortage || affected.Contains(order.SalesOrderId)))
                db.Notices.Add(new Notice { UserId = order.CreatedByUserId, Title = $"SO {order.Socode} thiếu nguồn", NoticeType = "WARNING",
                    Content = reason + " Mở đơn để bổ sung nguồn cho phần thiếu.",
                    CreatedAt = Now, RelatedUrl = $"/SalesOrder/Details/{order.SalesOrderId}" });
            order.HasShortage = shortage;
        }
        await db.SaveChangesAsync(ct);
    }

    private void Release(Reservation r, long quantity)
    {
        if (quantity <= 0) return;
        r.ReleasedQuantity += checked((int)quantity);
        if (Balance(r) == 0) { r.Status = "CLOSED"; r.ClosedAt = Now; }
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Cập nhật cam kết nguồn phải nằm trong transaction.");
    }

    private sealed record Claim(long SourceId, long? SalesOrderId, long? TransferOrderId);
}
