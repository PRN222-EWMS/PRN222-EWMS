using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Warehouse;

namespace PRN222_EWMS_Web.Services.Purchasing;

public class WarehouseCapacityEngine(EwmsDbContext db, ISourceReservationService sources, TimeProvider clock) : IWarehouseCapacityEngine
{
    private const int MaxLineQuantity = 1000000;
    private static readonly string[] LockedStatuses = ["COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL"];
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));

    private sealed record PositionedStock(long Id, int ProductId, string Condition, int? LocationId);
    private sealed class Snapshot
    {
        public Dictionary<int, Product> Products { get; set; } = [];
        public List<CapacitySlot> Slots { get; } = [];
        public List<PositionedStock> Serials { get; set; } = [];
        public List<CapacityLoad> Awaiting { get; set; } = [];
        public List<CapacityLoad> Purchases { get; } = [];
        public List<CapacityLoad> Transfers { get; } = [];
        public List<CapacityLoad> Returns { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
        public HashSet<int> LockedLocations { get; set; } = [];
        public Dictionary<int, StorageLocation> Locations { get; set; } = [];
        public bool AwaitingLocked { get; set; }
        public CapacityProduct Profile(int id)
        {
            var p = Products[id];
            return new(p.ProductId, p.ProductCode, p.CategoryId, p.BrandId, p.LengthCm, p.WidthCm, p.HeightCm, p.GrossWeightKg);
        }
        public List<CapacityLoad> Queue(IEnumerable<PositionedStock> serials) => serials
            .Where(s => s.LocationId == null).GroupBy(s => new { s.ProductId, s.Condition })
            .Select(g => new CapacityLoad(Profile(g.Key.ProductId), g.Key.Condition, g.LongCount())).ToList();
    }
    private sealed record Simulation(List<CapacitySlot> Slots, List<string> Errors,
        Dictionary<(int Slot, int Product, string Condition), long> BeforeRequests,
        Dictionary<(int Product, string Condition), long> PhysicalShortages,
        Dictionary<(int Product, string Condition), long> FutureShortages);

    public Task LockWarehouseAsync(int warehouseId, CancellationToken ct = default) => sources.LockWarehouseAsync(warehouseId, ct);

    private async Task<Snapshot> ReadAsync(WarehouseCapacityRequest request, CancellationToken ct,
        IReadOnlyCollection<long>? ignoredStocktakeIds = null)
    {
        var s = new Snapshot();
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == request.WarehouseId, ct);
        if (warehouse == null || warehouse.Status != "ACTIVE")
        {
            s.Errors.Add("Kho tiếp nhận không tồn tại hoặc đã ngừng hoạt động.");
            return s;
        }
        var arrival = request.ExpectedArrivalDate ?? Today;
        if (arrival < Today) s.Errors.Add("Ngày dự kiến nhận không được trong quá khứ.");
        if (warehouse.WarehouseType == "RENTED" &&
            (warehouse.RentalStartDate is not DateOnly start || warehouse.RentalEndDate is not DateOnly end || arrival < start || arrival > end))
            s.Errors.Add("Ngày nhận hàng nằm ngoài thời hạn thuê kho.");
        s.Products = await db.Products.AsNoTracking().Include(p => p.Brand).ToDictionaryAsync(p => p.ProductId, ct);
        foreach (var line in request.Demands)
            if (!s.Products.TryGetValue(line.ProductId, out var product) || product.Status != "ACTIVE" || product.Brand.Status != "ACTIVE")
                s.Errors.Add("Có sản phẩm/thương hiệu không tồn tại hoặc đã ngừng hoạt động.");
        if (s.Errors.Count > 0) return s;

        var locations = await db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == request.WarehouseId).ToListAsync(ct);
        s.Locations = locations.ToDictionary(l => l.LocationId);
        var rules = await db.ZoneStorageRules.AsNoTracking().Where(r => r.ZoneLocation.WarehouseId == request.WarehouseId).ToListAsync(ct);
        s.Serials = await db.SerialItems.AsNoTracking().Where(i => i.CurrentWarehouseId == request.WarehouseId
                && (i.StockState == "IN_WAREHOUSE" || i.StockState == "RECEIVED"))
            .Select(i => new PositionedStock(i.SerialItemId, i.ProductId, i.ItemCondition, i.CurrentLocationId)).ToListAsync(ct);
        foreach (var leaf in locations.Where(l => l.LocationType is "TIER" or "FLOOR_SLOT"))
        {
            var chain = new List<StorageLocation>();
            var seen = new HashSet<int>();
            StorageLocation? node = leaf;
            while (node != null && seen.Add(node.LocationId) && chain.Count < 5)
            {
                chain.Add(node);
                node = node.ParentLocationId is int id && s.Locations.TryGetValue(id, out var parent) ? parent : null;
            }
            bool floor = leaf.LocationType == "FLOOR_SLOT";
            if (chain.Any(l => l.Status != "ACTIVE") || chain[^1].ParentLocationId != null) continue;
            if (floor ? chain.Count != 3 || chain[1].LocationType != "ROW" || chain[1].StorageType != "FLOOR"
                : chain.Count != 4 || chain[1].LocationType != "RACK" || chain[2].LocationType != "ROW" || chain[2].StorageType != "RACK") continue;
            var zone = chain[^1];
            var condition = floor ? leaf.ConditionPolicy : chain[1].ConditionPolicy;
            if (zone.LocationType != "ZONE" || zone.ZoneType is not ("STANDARD" or "RESERVE") || condition is not ("GOOD" or "DAMAGED")
                || leaf.LengthCm is not > 0 || leaf.WidthCm is not > 0 || (!floor && leaf.HeightCm is not > 0)
                || leaf.HeightCm is <= 0 || leaf.MaxWeightKg is <= 0 || leaf.CapacityMode != (floor ? "FLOOR_AREA" : "VOLUME")) continue;
            s.Slots.Add(new CapacitySlot
            {
                Id = leaf.LocationId, Reserve = zone.ZoneType == "RESERVE", Floor = floor, Condition = condition,
                Length = leaf.LengthCm.Value, Width = leaf.WidthCm.Value, Height = leaf.HeightCm, MaxWeight = leaf.MaxWeightKg,
                Rules = rules.Where(r => r.ZoneLocationId == zone.LocationId).Select(r => (r.CategoryId, r.BrandId)).ToList(),
                Loads = s.Serials.Where(i => i.LocationId == leaf.LocationId).GroupBy(i => new { i.ProductId, i.Condition })
                    .Select(g => new CapacityLoad(s.Profile(g.Key.ProductId), g.Key.Condition, g.LongCount())).ToList()
            });
        }
        s.Awaiting = s.Queue(s.Serials);
        var purchases = await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrder.WarehouseId == request.WarehouseId
                && (request.ExcludePoId == null || l.PurchaseOrderId != request.ExcludePoId)
                && (l.PurchaseOrder.Status == "CONFIRMED" || l.PurchaseOrder.Status == "PARTIALLY_RECEIVED"))
            .Select(l => new { l.ProductId, Qty = (long)l.OrderedQuantity - l.ClosedQuantity
                - db.InboundItems.LongCount(i => i.PolineId == l.PolineId && i.IsActive && i.Inbound.Status == "DONE") }).ToListAsync(ct);
        s.Purchases.AddRange(purchases.Where(l => l.Qty > 0).Select(l => new CapacityLoad(s.Profile(l.ProductId), "GOOD", l.Qty)));

        var transfers = await db.TransferOrderLines.AsNoTracking().Where(l => l.TransferOrder.ToWarehouseId == request.WarehouseId
                && (request.ExcludeTransferOrderId == null || l.TransferOrderId != request.ExcludeTransferOrderId)
                && (l.TransferOrder.Status == "RELEASED" || l.TransferOrder.Status == "IN_PROGRESS"))
            .Select(l => new { l.ProductId, l.TransferOrder.ItemCondition, l.TransferOrder.Status, l.TransferOrder.ExpectedDate,
                HasDocuments = l.TransferOrder.TransferDispatches.Any() || l.TransferOrder.TransferReceipts.Any(),
                Qty = (long)l.RequestedQuantity - l.CancelledQuantity
                    - db.TransferReceiptItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.TransferReceipt.Status == "DONE")
                    - db.TransferLossRequestItems.LongCount(i => i.DispatchItem.TransferLineId == l.TransferLineId && i.IsActive && i.LossRequest.AppliedAt != null) }).ToListAsync(ct);
        s.Transfers.AddRange(transfers.Where(l => l.Qty > 0 && !TransferPolicy.IsExpired(l.Status, l.ExpectedDate, Today, l.HasDocuments))
            .Select(l => new CapacityLoad(s.Profile(l.ProductId), l.ItemCondition, l.Qty)));
        var returns = await db.CustomerReturnOrderLines.AsNoTracking().Where(l => l.ReturnOrder.WarehouseId == request.WarehouseId
                && (request.ExcludeReturnOrderId == null || l.ReturnOrderId != request.ExcludeReturnOrderId)
                && (l.ReturnOrder.Status == "OPEN" || l.ReturnOrder.Status == "PARTIALLY_RECEIVED"))
            .Select(l => new { l.ProductId, Qty = (long)l.RequestedQuantity - l.CancelledQuantity
                - db.ReturnReceiptItems.LongCount(i => i.ReturnLineId == l.ReturnLineId && i.IsActive && i.ReturnReceipt.Status == "DONE") }).ToListAsync(ct);
        s.Returns.AddRange(returns.Where(l => l.Qty > 0).Select(l => new CapacityLoad(s.Profile(l.ProductId), "UNKNOWN", l.Qty)));
        // Document remainders own future space. SupplySource/Reservation/SourcePlan are not additional arrivals.
        var ignored = ignoredStocktakeIds?.ToArray() ?? [];
        var locked = await db.Stocktakes.AsNoTracking().Where(t => t.WarehouseId == request.WarehouseId
                && LockedStatuses.Contains(t.Status) && !ignored.Contains(t.StocktakeId))
            .Select(t => new { t.StocktakeId, t.ScopeType, t.IncludeAwaitingPutaway }).ToListAsync(ct);
        var ids = locked.Select(t => t.StocktakeId).ToArray();
        var scopes = await db.StocktakeScopes.AsNoTracking().Where(t => ids.Contains(t.StocktakeId))
            .Select(t => new { t.StocktakeId, t.LocationId }).ToListAsync(ct);
        s.LockedLocations = scopes.Select(t => t.LocationId).ToHashSet();
        s.AwaitingLocked = locked.Any(t => t.ScopeType == "WHOLE_WAREHOUSE" || t.IncludeAwaitingPutaway
            || t.ScopeType is not ("WHOLE_WAREHOUSE" or "LOCATIONS")
            || (t.ScopeType == "LOCATIONS" && scopes.All(x => x.StocktakeId != t.StocktakeId)));
        if (locked.Any(t => t.ScopeType != "LOCATIONS" || scopes.All(x => x.StocktakeId != t.StocktakeId)))
            s.LockedLocations.UnionWith(locations.Select(l => l.LocationId));
        if (locked.Count > 0)
            s.Warnings.Add("Kho có phạm vi đang kiểm kê. Sức chứa kế hoạch không đồng nghĩa có thể xếp ngay vào vị trí đang khóa.");
        if (s.Returns.Count > 0 || request.Demands.Any(l => l.Condition == "UNKNOWN"))
            s.Warnings.Add("Hàng khách trả chưa nhận được kiểm tra ở cả hai khả năng hàng tốt và hàng hỏng.");
        return s;
    }

    private static string[] Scenarios(Snapshot s, IEnumerable<CapacityDemand> demands) =>
        s.Returns.Count > 0 || demands.Any(l => l.Condition == "UNKNOWN") ? ["GOOD", "DAMAGED"] : ["GOOD"];

    private static Simulation Simulate(Snapshot s, IReadOnlyList<CapacityDemand> demands, string scenario,
        bool commitments = true, List<CapacitySlot>? slotsOverride = null, List<CapacityLoad>? queueOverride = null)
    {
        var slots = (slotsOverride ?? s.Slots).Select(l => l.Copy()).ToList();
        var physicalShortages = new Dictionary<(int Product, string Condition), long>();
        var futureShortages = new Dictionary<(int Product, string Condition), long>();
        var errors = TransferCapacityPlanner.Allocate(slots, queueOverride ?? s.Awaiting, physicalShortages);
        if (commitments)
            errors.AddRange(TransferCapacityPlanner.Allocate(slots, s.Purchases.Concat(s.Transfers)
                .Concat(s.Returns.Select(l => l with { Condition = scenario })), futureShortages));
        var before = slots.SelectMany(slot => slot.Loads.Select(l => new { Slot = slot.Id, Product = l.Product.Id, l.Condition, l.Quantity }))
            .GroupBy(l => (l.Slot, l.Product, l.Condition))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        if (errors.Count == 0)
            errors.AddRange(TransferCapacityPlanner.Allocate(slots, demands.Where(l => l.Quantity > 0)
                .Select(l => new CapacityLoad(s.Profile(l.ProductId), l.Condition == "UNKNOWN" ? scenario : l.Condition, l.Quantity))));
        return new(slots, errors, before, physicalShortages, futureShortages);
    }

    private static bool DoesNotWorsen(Simulation baseline, Simulation candidate) =>
        candidate.PhysicalShortages.All(s => s.Value <= baseline.PhysicalShortages.GetValueOrDefault(s.Key))
        && candidate.FutureShortages.All(s => s.Value <= baseline.FutureShortages.GetValueOrDefault(s.Key));

    private static List<string> Errors(Snapshot s, IReadOnlyList<CapacityDemand> demands, bool commitments = true)
    {
        var scenarios = Scenarios(s, demands);
        return scenarios.SelectMany(condition => Simulate(s, demands, condition, commitments).Errors
            .Select(e => scenarios.Length == 1 ? e : $"Khả năng khách trả {(condition == "GOOD" ? "hàng tốt" : "hàng hỏng")}: {e}"))
            .Distinct().Take(8).ToList();
    }

    public async Task<WarehouseCapacityAssessment> EvaluateAsync(WarehouseCapacityRequest request, CancellationToken ct = default)
    {
        var result = new WarehouseCapacityAssessment();
        if (request.WarehouseId <= 0 || request.Demands.Count is < 1 or > 100
            || request.Demands.Any(l => l.ProductId <= 0 || l.Quantity is < 1 or > MaxLineQuantity || l.Condition is not ("GOOD" or "DAMAGED" or "UNKNOWN")))
        {
            result.Errors.Add("Cần kho hợp lệ và từ 1 đến 100 dòng, số lượng nguyên dương tối đa 1.000.000 mỗi dòng.");
            return result;
        }
        var demands = request.Demands.GroupBy(l => new { l.ProductId, l.Condition })
            .Select(g => new CapacityDemand(g.Key.ProductId, g.Sum(l => l.Quantity), g.Key.Condition)).ToList();
        if (demands.Any(l => l.Quantity > MaxLineQuantity))
        {
            result.Errors.Add("Tổng số lượng của một sản phẩm vượt quá giới hạn 1.000.000.");
            return result;
        }
        var s = await ReadAsync(request, ct);
        result.Errors.AddRange(s.Errors);
        result.Warnings.AddRange(s.Warnings);
        if (!result.IsValid) return result;
        result.IncomingPurchaseQuantity = s.Purchases.Sum(l => l.Quantity);
        result.IncomingTransferQuantity = s.Transfers.Sum(l => l.Quantity);
        result.IncomingReturnQuantity = s.Returns.Sum(l => l.Quantity);
        result.AwaitingPutawayQuantity = s.Awaiting.Sum(l => l.Quantity);
        result.PhysicalFits = Errors(s, demands, false).Count == 0;
        result.Errors.AddRange(Errors(s, demands));
        if (!request.CalculateLimits) return result;

        foreach (var demand in demands)
        {
            ct.ThrowIfCancellationRequested();
            var product = s.Products[demand.ProductId];
            var profile = s.Profile(demand.ProductId);
            var row = new ProductCapacityResult
            {
                ProductId = product.ProductId, ProductCode = product.ProductCode, ProductName = product.ProductName,
                HasValidZone = (demand.Condition == "UNKNOWN" ? new[] { "GOOD", "DAMAGED" } : [demand.Condition])
                    .All(condition => s.Slots.Any(slot => slot.Accepts(new(profile, condition, 1))
                        && TransferCapacityPlanner.Fits(slot, new(profile, condition, 1)))),
                SupplyPipelineQuantity = (int)Math.Min(int.MaxValue, s.Purchases.Concat(s.Transfers).Concat(s.Returns).Where(l => l.Product.Id == demand.ProductId).Sum(l => l.Quantity)),
                AwaitingPutawayQuantity = (int)Math.Min(int.MaxValue, s.Awaiting.Where(l => l.Product.Id == demand.ProductId).Sum(l => l.Quantity))
            };
            List<CapacityDemand> WithQuantity(long quantity) => demands.Select(l => l == demand ? l with { Quantity = quantity } : l).ToList();
            long low = result.IsValid ? demand.Quantity : 0, high = MaxLineQuantity;
            if (!row.HasValidZone || Errors(s, WithQuantity(0)).Count > 0) high = 0;
            low = Math.Min(low, high);
            while (low < high)
            {
                ct.ThrowIfCancellationRequested();
                long mid = low + (high - low + 1) / 2;
                if (Errors(s, WithQuantity(mid)).Count == 0) low = mid; else high = mid - 1;
            }
            row.MaxOrderQuantity = (int)low;
            row.IsValid = result.IsValid && demand.Quantity <= low;
            if (!row.IsValid) row.ErrorMessage = string.Join(" ", result.Errors);
            if (low > 0)
            {
                var run = Simulate(s, WithQuantity(low), Scenarios(s, demands)[0]);
                string condition = demand.Condition == "UNKNOWN" ? Scenarios(s, demands)[0] : demand.Condition;
                foreach (var slot in run.Slots)
                {
                    var count = slot.Loads.Where(l => l.Product.Id == demand.ProductId && l.Condition == condition).Sum(l => l.Quantity)
                        - run.BeforeRequests.GetValueOrDefault((slot.Id, demand.ProductId, condition));
                    if (slot.Floor) row.FloorSlotCapacity += (int)count; else row.RackTierCapacity += (int)count;
                }
            }
            result.Lines.Add(row);
        }
        return result;
    }

    public async Task<ProductCapacityResult> CalculateCapacityAsync(int warehouseId, int productId, int requestedQuantity,
        long? excludePoId = null, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(new() { WarehouseId = warehouseId, ExcludePoId = excludePoId,
            Demands = [new(productId, requestedQuantity)], CalculateLimits = true }, cancellationToken);
        return result.Lines.FirstOrDefault() ?? new() { ProductId = productId, ErrorMessage = string.Join(" ", result.Errors) };
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidatePurchaseOrderLinesCapacityAsync(int warehouseId,
        IEnumerable<(int ProductId, int Quantity)> lines, long? excludePoId = null, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(new() { WarehouseId = warehouseId, ExcludePoId = excludePoId,
            Demands = lines.Select(l => new CapacityDemand(l.ProductId, l.Quantity)).ToList() }, cancellationToken);
        return (result.IsValid, result.IsValid ? null : string.Join(" ", result.Errors));
    }

    public async Task<IReadOnlyDictionary<int, string>> GetPlacementErrorsAsync(int warehouseId, long serialId,
        IReadOnlyCollection<int> locationIds, CancellationToken ct = default) =>
        (await AssessPlacementsAsync(warehouseId, serialId, locationIds, ct)).Errors;

    public async Task<PlacementCapacityAssessment> AssessPlacementsAsync(int warehouseId, long serialId,
        IReadOnlyCollection<int> locationIds, CancellationToken ct = default)
    {
        var assessment = new PlacementCapacityAssessment();
        var errors = assessment.Errors;
        if (locationIds.Count == 0) return assessment;
        var s = await ReadAsync(new() { WarehouseId = warehouseId }, ct);
        var serial = s.Serials.SingleOrDefault(i => i.Id == serialId);
        if (s.Errors.Count > 0 || serial == null || serial.LocationId != null || s.AwaitingLocked)
        {
            string reason = s.Errors.FirstOrDefault() ?? "Serial không còn chờ xếp hoặc phạm vi chờ xếp đang bị khóa kiểm kê.";
            foreach (var id in locationIds) errors[id] = reason;
            return assessment;
        }
        return AssessPlacementCandidates(s, serial, serial.Condition, locationIds, ct);
    }

    public async Task<PlacementCapacityAssessment> AssessStocktakePlacementsAsync(int warehouseId, long serialId, string condition,
        IReadOnlyCollection<int> locationIds, IReadOnlyCollection<long> stocktakeIds, CancellationToken ct = default)
    {
        var assessment = new PlacementCapacityAssessment();
        if (locationIds.Count == 0) return assessment;
        var s = await ReadAsync(new() { WarehouseId = warehouseId }, ct, stocktakeIds);
        var serial = s.Serials.SingleOrDefault(i => i.Id == serialId);
        if (s.Errors.Count > 0 || serial == null || condition is not ("GOOD" or "DAMAGED")
            || stocktakeIds.Count == 0 || PlacementLocked(s, serial.LocationId))
        {
            var reason = s.Errors.FirstOrDefault() ?? "Serial hoặc tình trạng không hợp lệ, hoặc đang thuộc phạm vi kiểm kê khác.";
            foreach (var id in locationIds) assessment.Errors[id] = reason;
            return assessment;
        }
        return AssessPlacementCandidates(s, serial, condition, locationIds, ct);
    }

    private static bool PlacementLocked(Snapshot s, int? locationId)
    {
        if (locationId == null) return s.AwaitingLocked;
        var seen = new HashSet<int>();
        while (locationId is int id && seen.Add(id) && s.Locations.TryGetValue(id, out var node))
        {
            if (s.LockedLocations.Contains(id)) return true;
            locationId = node.ParentLocationId;
        }
        return false;
    }

    private static PlacementCapacityAssessment AssessPlacementCandidates(Snapshot s, PositionedStock serial, string condition,
        IReadOnlyCollection<int> locationIds, CancellationToken ct)
    {
        var assessment = new PlacementCapacityAssessment();
        var load = new CapacityLoad(s.Profile(serial.ProductId), condition, 1);
        var queue = s.Queue(s.Serials.Where(i => i.Id != serial.Id));
        var scenarios = Scenarios(s, []);
        var baselines = scenarios.ToDictionary(scenario => scenario, scenario => Simulate(s, [], scenario));
        var existingProblems = baselines.Values.SelectMany(run => run.Errors).Distinct().Take(3).ToList();
        if (existingProblems.Count > 0)
            assessment.Warnings.Add("Một số hàng chờ/sắp nhận đang thiếu chỗ. Các vị trí được phép chọn không làm tăng phần thiếu: "
                + string.Join(" ", existingProblems));
        foreach (var id in locationIds.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            var slots = s.Slots.Select(l => l.Copy()).ToList();
            // Remove this serial once from its registered place before simulating the new place/condition.
            // Staying in the same place therefore does not count the serial twice.
            var original = slots.SingleOrDefault(l => l.Id == serial.LocationId);
            var previous = original?.Loads.FindIndex(l => l.Product.Id == serial.ProductId && l.Condition == serial.Condition);
            if (original != null && previous is >= 0)
            {
                var old = original.Loads[previous.Value];
                if (old.Quantity == 1) original.Loads.RemoveAt(previous.Value);
                else original.Loads[previous.Value] = old with { Quantity = old.Quantity - 1 };
            }
            var target = slots.SingleOrDefault(l => l.Id == id);
            if (target == null || PlacementLocked(s, id) || !target.Accepts(load) || !TransferCapacityPlanner.Fits(target, load))
            {
                assessment.Errors[id] = "Vị trí không còn phù hợp, không đủ chỗ hoặc đang bị khóa.";
                continue;
            }
            target.Loads.Add(load);
            var problems = scenarios.Select(scenario => (Baseline: baselines[scenario],
                    Candidate: Simulate(s, [], scenario, true, slots, queue)))
                .Where(run => !DoesNotWorsen(run.Baseline, run.Candidate))
                .SelectMany(run => run.Candidate.Errors).Distinct().Take(3).ToList();
            if (problems.Count > 0) assessment.Errors[id] = "Vị trí này cần dành chỗ cho hàng chờ/sắp nhận: " + string.Join(" ", problems);
        }
        return assessment;
    }
}
