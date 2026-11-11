using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class OutboundSupplyService(EwmsDbContext db) : IOutboundSupplyService
{
    private static readonly string[] OpenOrders = ["SUBMITTED", "PARTIALLY_FULFILLED"];
    private static readonly string[] StocktakeLocks = ["COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL"];

    public async Task LockWarehouseAsync(int warehouseId, CancellationToken ct = default)
    {
        RequireTransaction();
        var resource = $"EWMS:WarehouseSupply:{warehouseId}";
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
            IF @result<0 THROW 51260,N'Không lấy được quyền cập nhật nguồn hàng. Vui lòng thử lại.',1;", ct);
        await db.SupplySources.FromSqlInterpolated($"SELECT * FROM dbo.SupplySource WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}")
            .AsNoTracking().Select(s => s.SupplySourceId).ToListAsync(ct);
        await db.Reservations.FromSqlInterpolated($"SELECT * FROM dbo.Reservation WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}")
            .AsNoTracking().Select(r => r.ReservationId).ToListAsync(ct);
    }

    public async Task ReconcileWarehouseAsync(int warehouseId, CancellationToken ct = default)
    {
        await LockWarehouseAsync(warehouseId, ct);
        var state = await LoadAsync(warehouseId, true, ct);
        ReleaseUnpickedPlans(state);
        UpdateShortages(state);
        await db.SaveChangesAsync(ct);
    }

    public async Task AfterInboundPostedAsync(long inboundId, CancellationToken ct = default)
    {
        RequireTransaction();
        var receipt = await db.Inbounds.AsNoTracking().Where(i => i.InboundId == inboundId)
            .Select(i => new { i.Status, WarehouseId = i.PurchaseOrder.WarehouseId }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy phiếu nhập để cập nhật nguồn hàng.");
        if (receipt.Status != "DONE") throw new InvalidOperationException("Phiếu nhập chưa hoàn tất.");
        // Incoming is a forecast, not an exclusive assignment to an SO. Received
        // GOOD remains available until a warehouse operator actually selects it.
        await ReconcileWarehouseAsync(receipt.WarehouseId, ct);
    }

    public async Task<OutboundSupplySnapshot> GetAvailabilityAsync(long salesOrderId, long? outboundId = null, CancellationToken ct = default)
    {
        var warehouseId = await db.SalesOrders.AsNoTracking().Where(o => o.SalesOrderId == salesOrderId)
            .Select(o => (int?)o.WarehouseId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy đơn bán hàng.");
        if (outboundId.HasValue && !await db.Outbounds.AsNoTracking().AnyAsync(o => o.OutboundId == outboundId && o.SalesOrderId == salesOrderId, ct))
            throw new InvalidOperationException("Phiếu xuất không thuộc đơn bán hàng.");
        var state = await LoadAsync(warehouseId, false, ct);
        var selected = outboundId.HasValue ? await db.OutboundItems.AsNoTracking()
            .Where(i => i.OutboundId == outboundId && i.IsActive)
            .Select(i => new { i.SerialItemId, i.SolineId, i.SourceAtIssueId }).ToListAsync(ct) : [];
        var selectedIds = selected.Select(i => i.SerialItemId).ToHashSet();
        var otherClaimIds = state.Claims.Where(c => !outboundId.HasValue || c.OutboundId != outboundId).Select(c => c.SerialItemId).ToHashSet();
        var locks = await GetLocksAsync(warehouseId, ct);
        var sources = state.Sources.ToDictionary(s => s.SupplySourceId);
        var candidates = state.Serials.Where(s => sources.TryGetValue(s.CurrentSupplySourceId!.Value, out var source)
            && IsPhysical(source) && !otherClaimIds.Contains(s.SerialItemId) && !locks.Blocks(s.CurrentLocationId)).ToList();
        var lines = new List<OutboundLineProgressDto>();
        var suggestions = new List<SuggestedSerialPickDto>();
        foreach (var line in state.Lines.Where(l => l.SalesOrderId == salesOrderId))
        {
            var remaining = Remaining(state, line);
            var budgets = new Dictionary<long, long>();
            foreach (var source in state.Sources.Where(s => s.ProductId == line.ProductId && IsPhysical(s)))
            {
                var selectable = candidates.LongCount(s => s.CurrentSupplySourceId == source.SupplySourceId);
                budgets[source.SupplySourceId] = Math.Min(selectable, CapacityForOrder(state, source, salesOrderId));
            }
            var maxNow = Math.Min(remaining, budgets.Values.Sum());
            var incomingNeed = Math.Max(0, remaining - maxNow);
            var breakdown = budgets.Where(pair => pair.Value > 0).Select(pair => new OutboundSourceBalanceDto(
                pair.Key, SourceCode(sources[pair.Key]), "PHYSICAL", pair.Value, false)).ToList();
            var incoming = 0L;
            foreach (var source in ForecastSources(state, line).OrderBy(s => s.AvailableFrom).ThenBy(s => s.SupplySourceId))
            {
                var take = Math.Min(incomingNeed - incoming, IncomingCapacity(source));
                if (take <= 0) continue;
                breakdown.Add(new OutboundSourceBalanceDto(source.SupplySourceId, SourceCode(source), "INCOMING", take, false));
                incoming += take;
            }
            var pickedLine = selected.Count(i => i.SolineId == line.SolineId);
            lines.Add(new OutboundLineProgressDto
            {
                SolineId = line.SolineId, ProductId = line.ProductId, ProductCode = line.Product.ProductCode,
                ProductName = line.Product.ProductName, Model = line.Product.Model, Unit = line.Product.Unit,
                RequestedQuantity = line.RequestedQuantity, CancelledQuantity = line.CancelledQuantity,
                DoneQuantity = state.Issued.GetValueOrDefault(line.SolineId), RemainingQuantity = remaining,
                ReservedBalance = state.Claims.LongCount(c => c.SalesOrderId == salesOrderId && c.ProductId == line.ProductId),
                PhysicalReservedBalance = state.Claims.LongCount(c => c.SalesOrderId == salesOrderId && c.ProductId == line.ProductId),
                IncomingReservedBalance = incoming, ShortageQuantity = Math.Max(0, remaining - maxNow - incoming),
                FallbackAvailableQuantity = 0, MaxIssueNow = maxNow, InThisReceiptCount = pickedLine, Sources = breakdown
            });
            var eligible = candidates.Where(s => s.ProductId == line.ProductId && budgets.GetValueOrDefault(s.CurrentSupplySourceId!.Value) > 0)
                .OrderBy(s => s.ManufacturingYear ?? short.MaxValue).ThenBy(s => s.ManufacturingMonth ?? byte.MaxValue)
                .ThenBy(s => s.OriginalInbound?.PurchaseOrder.OrderDate ?? DateOnly.MaxValue).ThenBy(s => s.SerialItemId).ToList();
            var priority = eligible.FirstOrDefault(s => !selectedIds.Contains(s.SerialItemId));
            foreach (var serial in eligible)
            {
                var sourceId = serial.CurrentSupplySourceId!.Value;
                var picked = selectedIds.Contains(serial.SerialItemId);
                var pickedSource = selected.Count(i => i.SolineId == line.SolineId && i.SourceAtIssueId == sourceId);
                suggestions.Add(new SuggestedSerialPickDto
                {
                    SerialItemId = serial.SerialItemId, SerialNumber = serial.SerialNumber,
                    ProductId = serial.ProductId, ProductCode = serial.Product.ProductCode, ProductName = serial.Product.ProductName,
                    ManufacturingMonth = serial.ManufacturingMonth, ManufacturingYear = serial.ManufacturingYear,
                    RegisteredAt = serial.RegisteredAt, ItemCondition = serial.ItemCondition,
                    LocationId = serial.CurrentLocationId, LocationPath = LocationPath(state.Locations, serial.CurrentLocationId),
                    SupplySourceId = sourceId, SourceCode = SourceCode(sources[sourceId]), IsFallback = false,
                    IsAlreadyPickedInSession = picked, IsPriority = serial.SerialItemId == priority?.SerialItemId,
                    CanSelect = !picked && pickedLine < maxNow && pickedSource < budgets[sourceId]
                });
            }
        }
        return new OutboundSupplySnapshot(lines, suggestions);
    }

    public async Task EnsureSourceReservationAsync(long salesOrderId, long lineId, long sourceId, int minimumBalance, CancellationToken ct = default)
    {
        RequireTransaction();
        if (minimumBalance <= 0) throw new InvalidOperationException("Số lượng lấy hàng không hợp lệ.");
        var warehouseId = await db.SalesOrders.Where(o => o.SalesOrderId == salesOrderId).Select(o => o.WarehouseId).SingleAsync(ct);
        await LockWarehouseAsync(warehouseId, ct);
        var state = await LoadAsync(warehouseId, true, ct);
        ReleaseUnpickedPlans(state);
        var line = state.Lines.SingleOrDefault(l => l.SolineId == lineId && l.SalesOrderId == salesOrderId)
            ?? throw new InvalidOperationException("Dòng hàng không thuộc SO.");
        if (!OpenOrders.Contains(line.SalesOrder.Status)) throw new InvalidOperationException("SO không còn mở để lấy hàng.");
        var source = state.Sources.SingleOrDefault(s => s.SupplySourceId == sourceId && s.ProductId == line.ProductId && IsPhysical(s))
            ?? throw new InvalidOperationException("Nguồn GOOD thực tế không còn hợp lệ.");
        if (minimumBalance > CapacityForOrder(state, source, salesOrderId) || minimumBalance > Remaining(state, line))
            throw new InvalidOperationException("Nguồn GOOD đang được phiên khác lấy hoặc không còn đủ số lượng cho phiếu này.");
        var existing = Active(state).Where(r => r.SolineId == lineId && r.SupplySourceId == sourceId).Sum(Balance);
        var additional = Math.Max(0, minimumBalance - existing);
        if (additional > 0)
        {
            var reservation = new Reservation
            {
                SupplySourceId = sourceId, SolineId = lineId, SalesOrderId = salesOrderId,
                ProductId = line.ProductId, WarehouseId = warehouseId, ItemCondition = "GOOD", SupplyState = "PHYSICAL",
                ReservedQuantity = checked((int)additional), ConsumedQuantity = 0, ReleasedQuantity = 0,
                Status = "ACTIVE", CreatedAt = DateTime.UtcNow
            };
            db.Reservations.Add(reservation);
            state.Reservations.Add(reservation);
        }
        UpdateShortages(state);
        await db.SaveChangesAsync(ct);
    }

    public async Task RefreshShortageAsync(long salesOrderId, CancellationToken ct = default)
    {
        RequireTransaction();
        var warehouseId = await db.SalesOrders.Where(o => o.SalesOrderId == salesOrderId).Select(o => o.WarehouseId).SingleAsync(ct);
        var state = await LoadAsync(warehouseId, true, ct);
        UpdateShortages(state);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SupplyState> LoadAsync(int warehouseId, bool tracking, CancellationToken ct)
    {
        IQueryable<SupplySource> sourceQuery = db.SupplySources.Where(s => s.WarehouseId == warehouseId);
        IQueryable<Reservation> reservationQuery = db.Reservations.Where(r => r.WarehouseId == warehouseId);
        IQueryable<SalesOrderLine> lineQuery = db.SalesOrderLines.Where(l => l.SalesOrder.WarehouseId == warehouseId);
        if (!tracking) { sourceQuery = sourceQuery.AsNoTracking(); reservationQuery = reservationQuery.AsNoTracking(); lineQuery = lineQuery.AsNoTracking(); }
        var sources = await sourceQuery.Include(s => s.Poline).ThenInclude(l => l!.PurchaseOrder)
            .Include(s => s.Inbound).ThenInclude(i => i!.PurchaseOrder).Include(s => s.ReturnReceipt)
            .Include(s => s.TransferReceipt).Include(s => s.Adjustment).AsSplitQuery().ToListAsync(ct);
        var reservations = await reservationQuery.ToListAsync(ct);
        var lines = await lineQuery.Include(l => l.Product).Include(l => l.SalesOrder).ToListAsync(ct);
        var serials = await db.SerialItems.AsNoTracking().Where(s => s.CurrentWarehouseId == warehouseId
                && s.CurrentSupplySourceId != null && s.CurrentSupplyState == "PHYSICAL" && s.StockState == "IN_WAREHOUSE"
                && s.ItemCondition == "GOOD" && !s.IsQuarantined)
            .Include(s => s.Product).Include(s => s.OriginalInbound).ThenInclude(i => i!.PurchaseOrder).AsSplitQuery().ToListAsync(ct);
        var claims = await db.PickingClaims.AsNoTracking().Where(c => c.SerialItem.CurrentWarehouseId == warehouseId)
            .Select(c => new SourceClaim(c.SerialItemId, c.SerialItem.CurrentSupplySourceId ?? 0, c.SerialItem.ProductId,
                c.OutboundId, c.DispatchId, c.Outbound == null ? null : c.Outbound.SalesOrderId)).ToListAsync(ct);
        var issued = await db.OutboundItems.AsNoTracking().Where(i => i.Outbound.SalesOrder.WarehouseId == warehouseId && i.IsActive && i.Outbound.Status == "DONE")
            .GroupBy(i => i.SolineId).Select(g => new { Id = g.Key, Count = g.LongCount() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);
        return new SupplyState(sources, reservations, lines, serials, claims, issued, locations);
    }

    private static void ReleaseUnpickedPlans(SupplyState state)
    {
        foreach (var group in Active(state).Where(r => r.SolineId.HasValue).GroupBy(r => new { r.SolineId, r.SupplySourceId }).ToList())
        {
            var line = state.Lines.SingleOrDefault(l => l.SolineId == group.Key.SolineId);
            var needed = line is null ? 0L : state.Claims.LongCount(c => c.SalesOrderId == line.SalesOrderId
                && c.ProductId == line.ProductId && c.SupplySourceId == group.Key.SupplySourceId && c.OutboundId.HasValue);
            foreach (var reservation in group.OrderBy(r => r.ReservationId))
            {
                // Keep only the physical balance backed by serials already selected.
                // Forecast PO quantities do not grant ownership to a sales order.
                var keep = reservation.SupplyState == "PHYSICAL" ? Math.Min(needed, Balance(reservation)) : 0;
                Release(reservation, Balance(reservation) - keep);
                needed -= keep;
            }
        }
    }

    private static long CapacityForOrder(SupplyState state, SupplySource source, long salesOrderId)
    {
        var capacity = PhysicalCapacity(state, source);
        var transferHeld = Active(state).Where(r => r.SupplySourceId == source.SupplySourceId
            && (r.TransferOrderId.HasValue || r.TransferLineId.HasValue)).Sum(Balance);
        var dispatchClaims = state.Claims.LongCount(c => c.SupplySourceId == source.SupplySourceId && c.DispatchId.HasValue);
        var otherSalesClaims = state.Claims.LongCount(c => c.SupplySourceId == source.SupplySourceId && !c.DispatchId.HasValue
            && c.SalesOrderId != salesOrderId);
        // Transfer claims already belong to transfer reservations: take the maximum,
        // not their sum. Other outbound claims are separate physical commitments.
        return Math.Max(0, capacity - Math.Max(transferHeld, dispatchClaims) - otherSalesClaims);
    }

    private static void UpdateShortages(SupplyState state)
    {
        foreach (var group in state.Lines.GroupBy(l => l.SalesOrderId))
        {
            var order = group.First().SalesOrder;
            if (!OpenOrders.Contains(order.Status))
            {
                if (order.Status is "FULFILLED" or "CLOSED" or "CANCELLED")
                    foreach (var line in group) line.SalesOrder.HasShortage = false;
                continue;
            }
            var shortage = group.Any(line => Remaining(state, line) > state.Sources
                .Where(s => s.ProductId == line.ProductId && IsPhysical(s)).Sum(s => CapacityForOrder(state, s, line.SalesOrderId))
                + ForecastSources(state, line).Sum(IncomingCapacity));
            foreach (var line in group) line.SalesOrder.HasShortage = shortage;
        }
    }

    private static IEnumerable<SupplySource> ForecastSources(SupplyState state, SalesOrderLine line) => state.Sources.Where(s =>
        s.ProductId == line.ProductId && IncomingCapacity(s) > 0 && (!line.SalesOrder.RequestedDate.HasValue
            || !s.AvailableFrom.HasValue || s.AvailableFrom.Value <= line.SalesOrder.RequestedDate.Value));
    private static void Release(Reservation reservation, long quantity)
    {
        if (quantity <= 0) return;
        reservation.ReleasedQuantity += checked((int)quantity);
        if (Balance(reservation) == 0) { reservation.Status = "CLOSED"; reservation.ClosedAt = DateTime.UtcNow; }
    }
    private static long Balance(Reservation reservation) => Math.Max(0, (long)reservation.ReservedQuantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity);
    private static IEnumerable<Reservation> Active(SupplyState state) => state.Reservations.Where(r => r.Status == "ACTIVE" && Balance(r) > 0);
    private static long Remaining(SupplyState state, SalesOrderLine line) => Math.Max(0, (long)line.RequestedQuantity - line.CancelledQuantity - state.Issued.GetValueOrDefault(line.SolineId));
    private static bool IsPhysical(SupplySource source) => source.SupplyState == "PHYSICAL" && source.ItemCondition == "GOOD" && (source.SourceType switch
    {
        "INBOUND" => source.Inbound?.Status == "DONE", "RETURN" => source.ReturnReceipt?.Status == "DONE",
        "TRANSFER_RECEIPT" or "TRANSFER" => source.TransferReceipt?.Status == "DONE",
        "ADJUSTMENT" => source.Adjustment?.Status == "APPLIED", _ => false
    });
    private static long PhysicalCapacity(SupplyState state, SupplySource source) => Math.Max(0,
        Math.Min(Math.Min(source.RemainingQuantity, source.EligibleQuantity), state.Serials.LongCount(s => s.CurrentSupplySourceId == source.SupplySourceId && s.ProductId == source.ProductId)));
    private static long IncomingCapacity(SupplySource source) => source.SupplyState == "INCOMING" && source.ItemCondition == "GOOD"
        && source.Poline?.PurchaseOrder.Status is "CONFIRMED" or "PARTIALLY_RECEIVED"
        ? Math.Max(0, Math.Min(source.RemainingQuantity, source.EligibleQuantity)) : 0;
    private static string SourceCode(SupplySource source) => source.SupplyState == "INCOMING" ? "Chờ PO " + (source.Poline?.PurchaseOrder.Pocode ?? $"#{source.PolineId}") : source.SourceType switch
    {
        "INBOUND" => $"Nhập {source.Inbound?.ReceiptCode} · PO {source.Inbound?.PurchaseOrder.Pocode}",
        "RETURN" => $"Nhận trả {source.ReturnReceipt?.ReceiptCode}",
        "TRANSFER_RECEIPT" or "TRANSFER" => $"Nhận chuyển {source.TransferReceipt?.ReceiptCode}",
        "ADJUSTMENT" => $"Điều chỉnh {source.Adjustment?.AdjustmentCode}", _ => $"Nguồn #{source.SupplySourceId}"
    };

    private async Task<LocationLocks> GetLocksAsync(int warehouseId, CancellationToken ct)
    {
        var active = await db.Stocktakes.AsNoTracking().Where(s => s.WarehouseId == warehouseId && StocktakeLocks.Contains(s.Status))
            .Select(s => new { s.StocktakeId, s.ScopeType, s.IncludeAwaitingPutaway }).ToListAsync(ct);
        var ids = active.Select(s => s.StocktakeId).ToArray();
        var scopes = await db.StocktakeScopes.AsNoTracking().Where(s => ids.Contains(s.StocktakeId)).Select(s => new { s.StocktakeId, s.LocationId }).ToListAsync(ct);
        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);
        var invalid = active.Any(s => s.ScopeType is not ("LOCATIONS" or "WHOLE_WAREHOUSE")
            || (s.ScopeType == "LOCATIONS" && scopes.All(r => r.StocktakeId != s.StocktakeId)))
            || scopes.Any(s => locations.All(l => l.LocationId != s.LocationId));
        return new LocationLocks(invalid || active.Any(s => s.ScopeType == "WHOLE_WAREHOUSE"),
            invalid || active.Any(s => s.IncludeAwaitingPutaway || s.ScopeType == "WHOLE_WAREHOUSE"),
            scopes.Select(s => s.LocationId).ToHashSet(), locations.ToDictionary(l => l.LocationId));
    }
    private static string LocationPath(List<StorageLocation> locations, int? id)
    {
        if (!id.HasValue) return "Hàng chờ xếp kho";
        var lookup = locations.ToDictionary(l => l.LocationId);
        var parts = new List<string>();
        var seen = new HashSet<int>();
        while (id.HasValue && seen.Add(id.Value) && lookup.TryGetValue(id.Value, out var location))
        { parts.Add(location.LocationName); id = location.ParentLocationId; }
        parts.Reverse();
        return string.Join(" → ", parts);
    }
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Cập nhật nguồn hàng phải nằm trong transaction.");
    }
    private sealed record SourceClaim(long SerialItemId, long SupplySourceId, int ProductId, long? OutboundId, long? DispatchId, long? SalesOrderId);
    private sealed record SupplyState(List<SupplySource> Sources, List<Reservation> Reservations, List<SalesOrderLine> Lines,
        List<SerialItem> Serials, List<SourceClaim> Claims, Dictionary<long, long> Issued, List<StorageLocation> Locations);
    private sealed record LocationLocks(bool Whole, bool Awaiting, HashSet<int> Roots, Dictionary<int, StorageLocation> Locations)
    {
        public bool Blocks(int? id)
        {
            if (Whole) return true;
            if (!id.HasValue) return Awaiting;
            var seen = new HashSet<int>();
            while (id.HasValue)
            {
                if (!seen.Add(id.Value) || !Locations.TryGetValue(id.Value, out var location)) return true;
                if (Roots.Contains(id.Value)) return true;
                id = location.ParentLocationId;
            }
            return false;
        }
    }
}
