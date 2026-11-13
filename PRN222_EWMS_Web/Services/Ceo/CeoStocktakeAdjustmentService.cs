using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.Services.Stocktaking;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Ceo;
using PRN222_EWMS_Web.ViewModels.Stocktaking;
using Plan = PRN222_EWMS_Web.Models.Stocktake;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Services.Ceo;

public class CeoStocktakeAdjustmentService(IStocktakeRepository repo, ISourceReservationService commitments) : ICeoStocktakeAdjustmentService
{
    private static readonly string[] Statuses = ["SUBMITTED", "REJECTED", "APPLIED", "APPROVED", "CANCELLED"];
    private async Task RequireCeoAsync(int actorId, CancellationToken ct)
    {
        if (!await repo.Query<User>().AsNoTracking().AnyAsync(u => u.UserId == actorId && u.Status == "ACTIVE" && u.Role.RoleName == "CEO", ct))
            throw new StocktakeWorkflowException("Tài khoản không còn quyền CEO.", 403);
    }

    public async Task<CeoStocktakeAdjustmentListViewModel> GetListAsync(int actorId, string? search, string? status, int page, CancellationToken ct)
    {
        await RequireCeoAsync(actorId, ct);
        search = search?.Trim();
        if (search?.Length > 100) throw new StocktakeWorkflowException("Từ khóa tối đa 100 ký tự.");
        if (!string.IsNullOrEmpty(status) && !Statuses.Contains(status)) throw new StocktakeWorkflowException("Trạng thái không hợp lệ.");
        var query = repo.Query<InventoryAdjustment>().AsNoTracking().Where(a => a.Status != "DRAFT");
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrEmpty(search)) query = query.Where(a => a.AdjustmentCode.Contains(search)
            || a.Stocktake.StocktakeCode.Contains(search) || a.Stocktake.Warehouse.WarehouseName.Contains(search));
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + CeoStocktakeAdjustmentListViewModel.PageSize - 1) / CeoStocktakeAdjustmentListViewModel.PageSize));
        var items = await query.Include(a => a.Stocktake).ThenInclude(s => s.Warehouse).Include(a => a.SubmittedByUser)
                .OrderBy(a => a.Status != "SUBMITTED").ThenByDescending(a => a.SubmittedAt).ThenByDescending(a => a.AdjustmentId)
                .Skip((page - 1) * CeoStocktakeAdjustmentListViewModel.PageSize).Take(CeoStocktakeAdjustmentListViewModel.PageSize).ToListAsync(ct);
        var ids = items.Select(a => a.AdjustmentId).ToArray();
        var totals = await repo.Query<InventoryAdjustmentLine>().AsNoTracking().Where(l => ids.Contains(l.AdjustmentId))
            .GroupBy(l => l.AdjustmentId).Select(g => new { Id = g.Key, Count = g.Count(), Delta = g.Sum(l => l.QuantityDelta) }).ToListAsync(ct);
        return new() { Search = search, Status = status, Page = page, TotalCount = total, Items = items,
            SerialCounts = totals.ToDictionary(t => t.Id, t => t.Count), QuantityChanges = totals.ToDictionary(t => t.Id, t => t.Delta) };
    }

    public async Task<CeoStocktakeAdjustmentDetailsViewModel?> GetDetailsAsync(int actorId, long id, CancellationToken ct)
    {
        await RequireCeoAsync(actorId, ct);
        var request = await repo.Query<InventoryAdjustment>().AsNoTracking().Include(a => a.Stocktake).ThenInclude(s => s.Warehouse)
            .Include(a => a.Stocktake).ThenInclude(s => s.CreatedByUser)
            .Include(a => a.SubmittedByUser).Include(a => a.DecidedByUser)
            .SingleOrDefaultAsync(a => a.AdjustmentId == id && a.Status != "DRAFT", ct);
        if (request is null) return null;
        var planId = request.StocktakeId;
        var lines = await repo.Query<InventoryAdjustmentLine>().AsNoTracking().Include(l => l.Product)
            .Include(l => l.Observation).ThenInclude(o => o.ScannedByUser)
            .Where(l => l.AdjustmentId == id).OrderBy(l => l.ScannedSerial).ToListAsync(ct);
        var observations = await repo.Query<StocktakeObservation>().AsNoTracking().Include(o => o.Product).Include(o => o.ScannedByUser)
            .Where(o => o.StocktakeId == planId).OrderBy(o => o.RoundNumber).ThenBy(o => o.ScannedSerial).ToListAsync(ct);
        var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Include(r => r.StartedByUser).Include(r => r.ClosedByUser)
            .Where(r => r.StocktakeId == planId).OrderBy(r => r.RoundNumber).ToListAsync(ct);
        var recounts = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == planId)
            .OrderBy(r => r.RequestedAt).ToListAsync(ct);
        var handling = await repo.HandlingAsync(planId, ct);
        var investigations = await repo.InvestigationsAsync(planId, ct);
        var serials = lines.Select(l => l.ScannedSerial).ToArray();
        var users = recounts.SelectMany(r => new[] { r.AssignedToUserId, r.RequestedByUserId })
            .Concat(investigations.Select(i => i.RequestedByUserId))
            .Concat(investigations.Where(i => i.ClosedByUserId.HasValue).Select(i => i.ClosedByUserId!.Value))
            .Concat(handling.Select(h => h.RecordedByUserId))
            .Concat(handling.Where(h => h.MovedByUserId.HasValue).Select(h => h.MovedByUserId!.Value)).Distinct().ToArray();
        var products = recounts.Select(r => r.ProductId).Concat(handling.Select(h => h.ProductId)).Distinct().ToArray();
        var model = new CeoStocktakeAdjustmentDetailsViewModel
        {
            Request = request, Lines = lines, Observations = observations,
            History = observations.Where(o => serials.Contains(o.ScannedSerial, StringComparer.OrdinalIgnoreCase)).ToList(),
            EffectiveResults = StocktakeScopeRules.EffectiveObservations(observations, rounds, recounts),
            Rounds = rounds, Recounts = recounts,
            Locations = await repo.Query<StorageLocation>().AsNoTracking().Where(l => l.WarehouseId == request.Stocktake.WarehouseId).ToListAsync(ct),
            Scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => s.StocktakeId == planId).ToListAsync(ct),
            Assignments = await repo.Query<StocktakeAssignment>().AsNoTracking().Include(a => a.User)
                .Where(a => a.StocktakeId == planId).OrderBy(a => a.AssignedAt).ThenBy(a => a.AssignmentId).ToListAsync(ct),
            Handling = handling, Investigations = investigations,
            UserNames = await repo.Query<User>().AsNoTracking().Where(u => users.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct),
            ProductNames = await repo.Query<Product>().AsNoTracking().Where(p => products.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, p => p.ProductName, ct),
            CurrentItems = await repo.Query<SerialItem>().AsNoTracking().Include(s => s.CurrentWarehouse)
                .Where(s => serials.Contains(s.SerialNumber)).ToListAsync(ct)
        };
        var itemIds = model.CurrentItems.Select(s => s.SerialItemId).ToArray();
        var planLocations = StocktakeScopeRules.PlanLocations(request.Stocktake, model.Scopes, model.Locations);
        foreach (var observation in observations)
        {
            var round = rounds.SingleOrDefault(r => r.RoundNumber == observation.RoundNumber);
            var recount = round is null ? null : recounts.SingleOrDefault(r => r.ResultRoundId == round.RoundId);
            model.CounterNames[observation.ObservationId] = observation.ScannedByUser?.FullName
                ?? (recount is not null ? model.UserNames.GetValueOrDefault(recount.AssignedToUserId, "Chưa có thông tin")
                    : string.Join(", ", model.Assignments.Where(a => a.Status != "CANCELLED"
                        && StocktakeScopeRules.InAssignment(a, observation.ExpectedQuantity == 1
                            ? observation.ExpectedLocationId : observation.ActualLocationId, planLocations, model.Locations))
                        .Select(a => a.User.FullName).Distinct()));
        }
        model.Movements = await repo.Query<SerialMovement>().AsNoTracking().Include(m => m.SerialItem).Include(m => m.PerformedByUser)
            .Include(m => m.FromWarehouse).Include(m => m.ToWarehouse).Include(m => m.FromLocation).Include(m => m.ToLocation)
            .Where(m => itemIds.Contains(m.SerialItemId) && m.OccurredAt >= (request.Stocktake.StartedAt ?? request.Stocktake.CreatedAt))
            .OrderBy(m => m.OccurredAt).ThenBy(m => m.MovementId).ToListAsync(ct);
        return model;
    }

    public async Task DecideAsync(int actorId, CeoStocktakeAdjustmentDecisionInput input, bool approve, CancellationToken ct)
    {
        await RequireCeoAsync(actorId, ct);
        var note = input.DecisionNote?.Trim();
        if (note?.Length > 1000 || (!approve && string.IsNullOrWhiteSpace(note)))
            throw new StocktakeWorkflowException("Từ chối phải có lý do; ghi chú tối đa 1.000 ký tự.");
        var warehouseId = await repo.Query<InventoryAdjustment>().AsNoTracking().Where(a => a.AdjustmentId == input.Id)
            .Select(a => (int?)a.Stocktake.WarehouseId).SingleOrDefaultAsync(ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy yêu cầu.", 404);
        await repo.TransactionAsync(actorId, warehouseId, async token =>
        {
            await RequireCeoAsync(actorId, token);
            var request = await repo.Query<InventoryAdjustment>().SingleOrDefaultAsync(a => a.AdjustmentId == input.Id, token)
                ?? throw new StocktakeWorkflowException("Không tìm thấy yêu cầu.", 404);
            if (request.Status != "SUBMITTED") throw new StocktakeWorkflowException("Yêu cầu đã được xử lý hoặc không còn chờ duyệt.", 409);
            byte[] version;
            try { version = Convert.FromBase64String(input.Version); }
            catch (FormatException) { throw new StocktakeWorkflowException("Phiên bản không hợp lệ. Hãy tải lại trang."); }
            if (!request.RowVersion.SequenceEqual(version)) throw new StocktakeWorkflowException("Yêu cầu vừa thay đổi. Hãy tải lại trang.", 409);
            var plan = await repo.Query<Plan>().SingleAsync(s => s.StocktakeId == request.StocktakeId, token);
            if (plan.WarehouseId != warehouseId || plan.Status != "PENDING_APPROVAL")
                throw new StocktakeWorkflowException("Đợt kiểm kê không còn chờ xét duyệt.", 409);
            if (await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == plan.StocktakeId && r.Status == "OPEN", token)
                || await repo.Query<StocktakeRecountRequest>().AnyAsync(r => r.StocktakeId == plan.StocktakeId && (r.Status == "REQUESTED" || r.Status == "IN_PROGRESS"), token))
                throw new StocktakeWorkflowException("Cần hoàn tất các yêu cầu đếm lại trước khi xét duyệt.");
            var lines = await repo.Query<InventoryAdjustmentLine>().Include(l => l.Observation).Include(l => l.Product)
                .Where(l => l.AdjustmentId == request.AdjustmentId && l.IsActive).ToListAsync(token);
            if (lines.Count == 0) throw new StocktakeWorkflowException("Yêu cầu không có chi tiết hợp lệ.");
            if (approve && lines.Any(l => l.AdjustmentType != "REMOVE"))
                throw new StocktakeWorkflowException("Chỉ duyệt giảm tồn cho hàng thiếu. Hãy từ chối phiếu cũ và xử lý theo chứng từ nguồn hoặc luồng chuyển hàng.");
            if (approve && await repo.HasInvestigationRestrictionAsync(plan.StocktakeId, token))
                throw new StocktakeWorkflowException("Cần kết luận đối soát và gửi đề nghị từ đợt kiểm kê gốc.");
            var now = DateTime.UtcNow;
            request.DecidedByUserId = actorId; request.DecidedAt = now; request.DecisionNote = note;
            if (!approve)
            {
                foreach (var line in lines) { line.IsActive = false; line.Observation.ReviewStatus = "PENDING"; }
                request.Status = "REJECTED";
                if (!await repo.Query<InventoryAdjustment>().AnyAsync(a => a.StocktakeId == plan.StocktakeId && a.AdjustmentId != request.AdjustmentId
                    && (a.Status == "SUBMITTED" || a.Status == "APPROVED"), token)) plan.Status = "REVIEWING";
                repo.TouchPlan(plan); await repo.SaveAsync(token); return true;
            }
            await ValidateEvidenceAsync(plan, lines, token);
            await commitments.LockWarehouseAsync(warehouseId, token);
            request.Status = "APPROVED";
            await repo.SaveAsync(token); // Posting triggers require a decision; all stages stay inside this transaction.
            var warehouse = await repo.Query<WarehouseEntity>().AsNoTracking().SingleAsync(w => w.WarehouseId == warehouseId, token);
            var today = DateOnly.FromDateTime(now.AddHours(7));
            if (warehouse.Status != "ACTIVE" || (warehouse.WarehouseType == "RENTED" &&
                (warehouse.RentalStartDate is not DateOnly start || warehouse.RentalEndDate is not DateOnly end || today < start || today > end)))
                throw new StocktakeWorkflowException("Kho phải đang hoạt động và còn hạn thuê để áp dụng điều chỉnh.");
            var locations = await repo.Query<StorageLocation>().AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(token);
            var changed = new List<(InventoryAdjustmentLine Line, SerialItem Item, SerialMovement Movement)>();
            var affectedSources = new HashSet<long>();
            foreach (var line in lines.OrderBy(l => l.ScannedSerial))
            {
                var item = await PrepareItemAsync(plan, line, token);
                var movement = new SerialMovement { SerialItem = item, PerformedByUserId = actorId, EventType = "STOCKTAKE_ADJUSTMENT",
                    OccurredAt = now, AdjustmentId = request.AdjustmentId, FromState = item.StockState,
                    FromCondition = item.ItemCondition,
                    FromWarehouseId = item.CurrentWarehouseId, FromLocationId = item.CurrentLocationId,
                    FromQuarantined = item.IsQuarantined, Note = note ?? request.Reason };
                if (item.CurrentSupplySourceId is long sourceId) affectedSources.Add(sourceId);
                item.StockState = "LOST"; item.CurrentWarehouseId = null; item.CurrentLocationId = null;
                item.CurrentSupplySourceId = null; item.CurrentSupplySource = null; item.CurrentSupplyState = null;
                item.IsQuarantined = false; item.QuarantineReason = null;
                movement.ToState = item.StockState; movement.ToCondition = item.ItemCondition;
                movement.ToWarehouseId = item.CurrentWarehouseId; movement.ToLocationId = item.CurrentLocationId; movement.ToQuarantined = item.IsQuarantined;
                changed.Add((line, item, movement));
            }
            await ValidateRemovalScopeAsync(plan, changed.Select(entry => entry.Movement), locations, token);
            foreach (var entry in changed) { repo.Add(entry.Movement); entry.Line.Observation.ReviewStatus = "RESOLVED"; }
            await repo.SaveAsync(token);
            foreach (var entry in changed) entry.Line.SerialItemId = entry.Item.SerialItemId;
            await ReconcileSourcesAsync(affectedSources, warehouseId, now, token);
            await repo.SaveAsync(token); // Save line IDs before marking the header immutable/APPLIED.
            request.Status = "APPLIED"; request.AppliedAt = now;
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    private async Task ValidateEvidenceAsync(Plan plan, List<InventoryAdjustmentLine> lines, CancellationToken ct)
    {
        if (lines.Select(l => l.ScannedSerial).Distinct(StringComparer.OrdinalIgnoreCase).Count() != lines.Count)
            throw new StocktakeWorkflowException("Yêu cầu có serial lặp.");
        var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == plan.StocktakeId).ToListAsync(ct);
        var requests = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == plan.StocktakeId).ToListAsync(ct);
        var all = await repo.Query<StocktakeObservation>().AsNoTracking().Where(o => o.StocktakeId == plan.StocktakeId).ToListAsync(ct);
        var effective = StocktakeScopeRules.EffectiveObservations(all, rounds, requests).Select(o => o.ObservationId).ToHashSet();
        foreach (var line in lines)
        {
            var o = line.Observation;
            if (line.StocktakeId != plan.StocktakeId || o.StocktakeId != plan.StocktakeId || !effective.Contains(o.ObservationId)
                || o.ProductId != line.ProductId || line.Product is null || o.ScannedSerial != line.ScannedSerial || o.ReviewStatus != "REVIEWED")
                throw new StocktakeWorkflowException("Chi tiết yêu cầu không còn khớp kết quả kiểm kê hiện hành.");
            if (line.AdjustmentType != "REMOVE" || line.QuantityDelta != -1
                || o.ExpectedQuantity != 1 || o.ActualQuantity != 0
                || line.TargetCondition is not null || line.TargetLocationId is not null)
                throw new StocktakeWorkflowException("Loại điều chỉnh hoặc giá trị đích không khớp bằng chứng kiểm kê.");
            if (await repo.Query<InventoryAdjustmentLine>().AnyAsync(l => l.StocktakeId == plan.StocktakeId && l.AdjustmentId != line.AdjustmentId
                && l.ScannedSerial == line.ScannedSerial && l.IsActive && l.Adjustment.Status == "APPLIED", ct))
                throw new StocktakeWorkflowException("Serial đã được áp dụng điều chỉnh trong đợt này.", 409);
        }
    }

    private async Task<SerialItem> PrepareItemAsync(Plan plan, InventoryAdjustmentLine line, CancellationToken ct)
    {
        var item = await repo.Query<SerialItem>().Include(s => s.Product).Include(s => s.CurrentSupplySource)
            .SingleOrDefaultAsync(s => s.SerialNumber == line.ScannedSerial, ct);
        var o = line.Observation;
        var handling = (await repo.HandlingAsync(plan.StocktakeId, ct)).SingleOrDefault(h =>
            string.Equals(h.ScannedSerial, line.ScannedSerial, StringComparison.OrdinalIgnoreCase) && h.Status == "MOVED");
        if (item is null || item.SerialItemId != line.SerialItemId || item.ProductId != line.ProductId
            || item.StockState != "IN_WAREHOUSE" || item.CurrentWarehouseId != plan.WarehouseId
            || item.CurrentLocationId != (handling?.TargetLocationId ?? o.ExpectedLocationId)
            || item.ItemCondition != (handling?.ActualCondition ?? o.ExpectedCondition))
            throw new StocktakeWorkflowException($"Serial {line.ScannedSerial} đã thay đổi so với sổ kho ban đầu. Cần rà soát lại trước khi áp dụng.", 409);
        if (await repo.Query<PickingClaim>().AnyAsync(c => c.SerialItemId == item.SerialItemId, ct))
            throw new StocktakeWorkflowException($"Serial {line.ScannedSerial} đang có công việc xuất/chuyển kho.");
        var allowedMovement = handling?.MovementId;
        if (await repo.Query<SerialMovement>().AnyAsync(m => m.SerialItemId == item.SerialItemId && m.OccurredAt > o.CapturedAt && m.MovementId != allowedMovement, ct))
            throw new StocktakeWorkflowException($"Serial {line.ScannedSerial} đã có biến động sau khi bắt đầu kiểm kê. Cần xác minh lại.", 409);
        var source = item.CurrentSupplySource;
        if (source is null || source.SupplyState != "PHYSICAL" || source.WarehouseId != plan.WarehouseId
            || source.ProductId != item.ProductId || source.ItemCondition != item.ItemCondition)
            throw new StocktakeWorkflowException($"Nguồn hàng của {line.ScannedSerial} không hợp lệ.");
        return item;
    }

    private async Task ValidateRemovalScopeAsync(Plan plan, IEnumerable<SerialMovement> movements,
        List<StorageLocation> locations, CancellationToken ct)
    {
        var otherPlans = await repo.Query<Plan>().AsNoTracking().Where(s => s.WarehouseId == plan.WarehouseId && s.StocktakeId != plan.StocktakeId
            && StocktakeScopeRules.LockedStatuses.Contains(s.Status)).ToListAsync(ct);
        var scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => s.Stocktake.WarehouseId == plan.WarehouseId).ToListAsync(ct);
        foreach (var other in otherPlans)
        {
            var locked = StocktakeScopeRules.PlanLocations(other, scopes.Where(s => s.StocktakeId == other.StocktakeId).ToList(), locations);
            if (movements.Any(movement => StocktakeScopeRules.InPlan(other, locked, movement.FromLocationId)))
                throw new StocktakeWorkflowException("Vị trí đang thuộc đợt kiểm kê khác chưa hoàn tất.");
        }
    }

    private async Task ReconcileSourcesAsync(HashSet<long> ids, int warehouseId, DateTime now, CancellationToken ct)
    {
        foreach (var id in ids)
        {
            var source = await repo.Query<SupplySource>().SingleAsync(s => s.SupplySourceId == id, ct);
            var items = await repo.Query<SerialItem>().AsNoTracking().Where(s => s.CurrentSupplySourceId == id && s.StockState == "IN_WAREHOUSE").ToListAsync(ct);
            source.RemainingQuantity = items.Count;
            source.EligibleQuantity = items.Count;
            var reservations = await repo.Query<Reservation>().Where(r => r.SupplySourceId == id && r.Status == "ACTIVE")
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReservationId).ToListAsync(ct);
            var excess = Math.Max(0, reservations.Sum(r => r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity) - source.EligibleQuantity);
            if (excess > 0 && await repo.Query<PickingClaim>().AnyAsync(c => c.SerialItem.CurrentSupplySourceId == id, ct))
                throw new StocktakeWorkflowException("Nguồn bị giảm đang có công việc picking. Cần xử lý công việc đó trước khi điều chỉnh.");
            if (excess > 0 && await repo.Query<DemandAllocation>().AnyAsync(a => a.SupplySourceId == id
                && ((a.Outbound != null && a.Outbound.Status != "DONE" && a.Outbound.Status != "CANCELLED")
                    || (a.Dispatch != null && a.Dispatch.Status != "DONE" && a.Dispatch.Status != "CANCELLED")), ct))
                throw new StocktakeWorkflowException("Nguồn bị giảm đang được phân bổ cho phiếu xuất/chuyển kho chưa hoàn tất. Cần xử lý phiếu đó trước.");
        }
        await repo.SaveAsync(ct);
        await commitments.ReconcileWarehouseAsync(warehouseId,
            "Điều chỉnh kiểm kê đã áp dụng, nguồn bảo đảm cho đơn đã thay đổi.", ct);
    }
}
