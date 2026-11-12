using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.Services.WarehouseStaff;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Putaway;
using PRN222_EWMS_Web.ViewModels.Stocktaking;
using Plan = PRN222_EWMS_Web.Models.Stocktake;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public class StocktakeHandlingService(IStocktakeRepository repo, ISourceReservationService commitments) : IStocktakeHandlingService
{
    private async Task<(Plan Plan, StocktakeHandling Handling, SerialItem Item)> ContextAsync(int actorId, long id, long handlingId, CancellationToken ct)
    {
        var actor = await repo.Query<User>().AsNoTracking().SingleOrDefaultAsync(u => u.UserId == actorId
            && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF", ct)
            ?? throw new StocktakeWorkflowException("Tài khoản không còn quyền nhân viên kho.", 403);
        var plan = await repo.Query<Plan>().SingleOrDefaultAsync(s => s.StocktakeId == id && s.WarehouseId == actor.WarehouseId, ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy nhiệm vụ trong kho được giao.", 404);
        var handling = (await repo.HandlingAsync(id, ct)).SingleOrDefault(h => h.HandlingId == handlingId)
            ?? throw new StocktakeWorkflowException("Không tìm thấy ghi nhận cần chuyển.", 404);
        if (handling.RecordedByUserId != actorId)
            throw new StocktakeWorkflowException("Chỉ nhân viên ghi nhận bất thường được xác nhận chuyển hàng này.", 403);
        var item = await repo.Query<SerialItem>().Include(s => s.Product).Include(s => s.CurrentSupplySource)
            .SingleAsync(s => s.SerialItemId == handling.SerialItemId, ct);
        return (plan, handling, item);
    }

    public async Task<StocktakeHandlingViewModel> GetAsync(int actorId, long id, long handlingId, long? recountRequestId, CancellationToken ct)
    {
        var (plan, handling, item) = await ContextAsync(actorId, id, handlingId, ct);
        var locations = await repo.Query<StorageLocation>().AsNoTracking().Where(l => l.WarehouseId == plan.WarehouseId).ToListAsync(ct);
        var model = new StocktakeHandlingViewModel { Plan = plan, Handling = handling, Locations = locations,
            ProductName = item.Product.ProductName, Version = Convert.ToBase64String(plan.RowVersion),
            HandlingVersion = Convert.ToBase64String(handling.RowVersion), RecountRequestId = recountRequestId };
        model.CanMove = handling.Status == "WAITING" && StocktakeScopeRules.LockedStatuses.Contains(plan.Status)
            && (item.ItemCondition != "DAMAGED" || handling.ActualCondition != "GOOD");
        if (!model.CanMove) { model.Message = handling.Status == "MOVED" ? "Đã xác nhận chuyển hàng." : "Đợt không còn cho phép chuyển hàng."; return model; }
        if (await repo.Query<InventoryAdjustmentLine>().AnyAsync(l => l.StocktakeId == id && l.ScannedSerial == handling.ScannedSerial
            && l.IsActive && (l.Adjustment.Status == "DRAFT" || l.Adjustment.Status == "SUBMITTED" || l.Adjustment.Status == "APPROVED" || l.Adjustment.Status == "APPLIED"), ct))
        { model.CanMove = false; model.Message = "Serial đang nằm trong đề nghị điều chỉnh cũ. Manager cần xử lý đề nghị đó trước."; return model; }
        model.Suggestions = await SuggestionsAsync(plan, handling, item, locations, ct);
        if (model.Suggestions.Count == 0) model.Message = "Không có ô phù hợp hoặc đủ chỗ. Giữ trạng thái chờ xử lý và báo Manager bố trí vị trí hợp lệ.";
        return model;
    }

    public async Task MoveAsync(int actorId, StocktakeMoveInput input, CancellationToken ct)
    {
        if (!input.Confirmed) throw new StocktakeWorkflowException("Cần xác nhận đã chuyển hàng thực tế đến vị trí đã chọn.");
        var warehouseId = await repo.Query<Plan>().AsNoTracking().Where(s => s.StocktakeId == input.Id)
            .Select(s => (int?)s.WarehouseId).SingleOrDefaultAsync(ct) ?? throw new StocktakeWorkflowException("Không tìm thấy đợt.", 404);
        await repo.TransactionAsync(actorId, warehouseId, async token =>
        {
            await commitments.LockWarehouseAsync(warehouseId, token);
            var (plan, handling, item) = await ContextAsync(actorId, input.Id, input.HandlingId, token);
            if (handling.Status == "MOVED") return true; // Idempotent confirmation, no second movement.
            CheckVersion(plan.RowVersion, input.Version); CheckVersion(handling.RowVersion, input.HandlingVersion);
            var page = await GetAsync(actorId, input.Id, input.HandlingId, input.RecountRequestId, token);
            if (!page.CanMove) throw new StocktakeWorkflowException(page.Message ?? "Không thể chuyển hàng lúc này.");
            if (input.TargetLocationId is not int target || !page.Suggestions.Any(s => s.Id == target))
                throw new StocktakeWorkflowException("Vị trí đích không còn phù hợp hoặc không đủ chỗ. Hãy tải lại đề xuất.", 409);
            var evidence = await repo.Query<StocktakeObservation>().SingleAsync(o => o.ObservationId == handling.ObservationId, token);
            if (item.StockState != "IN_WAREHOUSE" || item.CurrentWarehouseId != warehouseId || item.ProductId != handling.ProductId
                || item.CurrentLocationId != handling.OriginalLocationId || item.SerialNumber != handling.ScannedSerial)
                throw new StocktakeWorkflowException("Hàng đã thay đổi so với khi ghi nhận. Cần rà soát lại trước khi chuyển.", 409);
            if (await repo.Query<PickingClaim>().AnyAsync(c => c.SerialItemId == item.SerialItemId, token))
                throw new StocktakeWorkflowException("Serial đang có công việc xuất/chuyển kho, chưa thể chuyển vị trí.");
            if (await repo.Query<SerialMovement>().AnyAsync(m => m.SerialItemId == item.SerialItemId && m.OccurredAt > evidence.CapturedAt, token))
                throw new StocktakeWorkflowException("Serial đã có biến động sau mốc kiểm kê. Manager cần rà soát lại.", 409);
            var now = DateTime.UtcNow;
            var movement = new SerialMovement { SerialItemId = item.SerialItemId, PerformedByUserId = actorId,
                EventType = "RELOCATION", OccurredAt = now, FromState = "IN_WAREHOUSE", ToState = "IN_WAREHOUSE",
                FromWarehouseId = warehouseId, ToWarehouseId = warehouseId, FromLocationId = handling.FoundLocationId,
                ToLocationId = target, FromCondition = item.ItemCondition, ToCondition = handling.ActualCondition,
                FromQuarantined = item.IsQuarantined, ToQuarantined = item.IsQuarantined,
                Note = $"Kiểm kê {plan.StocktakeCode}; phát hiện tại vị trí {handling.FoundLocationId?.ToString() ?? "chờ xếp"}; xác nhận chuyển; không thay đổi số lượng." };
            if (item.ItemCondition != handling.ActualCondition)
            {
                var oldSource = item.CurrentSupplySource;
                if (oldSource is null || oldSource.SupplyState != "PHYSICAL" || oldSource.WarehouseId != warehouseId
                    || oldSource.ProductId != item.ProductId || oldSource.ItemCondition != item.ItemCondition)
                    throw new StocktakeWorkflowException("Nguồn hàng hiện tại không khớp serial.");
                var source = await ConditionSourceAsync(oldSource, handling.ActualCondition, now, token);
                item.ItemCondition = handling.ActualCondition; item.CurrentSupplySourceId = source.SupplySourceId; item.CurrentSupplySource = source;
                item.CurrentSupplyState = "PHYSICAL";
                item.CurrentLocationId = target;
                await repo.SaveAsync(token);
                await ReconcileAsync(oldSource.SupplySourceId, now, token);
                await ReconcileAsync(source.SupplySourceId, now, token);
            }
            else item.CurrentLocationId = target;
            repo.Add(movement); await repo.SaveAsync(token);
            handling.TargetLocationId = target; handling.Status = "MOVED"; handling.MovedByUserId = actorId;
            handling.MovedAt = now; handling.MovementId = movement.MovementId;
            repo.Record(handling, actorId, isNew: false);
            // Keep the first count evidence (found place/condition) intact, including closed rounds.
            evidence.ReviewStatus = "RESOLVED";
            var sameSerial = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == plan.StocktakeId
                && o.ScannedSerial == handling.ScannedSerial).ToListAsync(token);
            foreach (var row in sameSerial) row.ReviewStatus = "RESOLVED";
            repo.TouchPlan(plan); await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    private async Task<List<PutawayLocationViewModel>> SuggestionsAsync(Plan plan, StocktakeHandling handling, SerialItem item, List<StorageLocation> locations, CancellationToken ct)
    {
        var warehouse = await repo.Query<WarehouseEntity>().AsNoTracking().SingleAsync(w => w.WarehouseId == plan.WarehouseId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (warehouse.Status != "ACTIVE" || warehouse.WarehouseType == "RENTED" &&
            (warehouse.RentalStartDate is not DateOnly start || warehouse.RentalEndDate is not DateOnly end || today < start || today > end)) return [];
        if (item.Product.Status != "ACTIVE" || item.StockState != "IN_WAREHOUSE" || item.CurrentWarehouseId != plan.WarehouseId) return [];
        var otherPlans = await repo.Query<Plan>().AsNoTracking().Where(s => s.WarehouseId == plan.WarehouseId && s.StocktakeId != plan.StocktakeId
            && StocktakeScopeRules.LockedStatuses.Contains(s.Status)).ToListAsync(ct);
        var ids = otherPlans.Select(p => p.StocktakeId).ToArray();
        var scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => ids.Contains(s.StocktakeId)).ToListAsync(ct);
        var blocked = new HashSet<int>();
        foreach (var other in otherPlans)
        {
            var locked = StocktakeScopeRules.PlanLocations(other, scopes.Where(s => s.StocktakeId == other.StocktakeId).ToList(), locations);
            if (StocktakeScopeRules.InPlan(other, locked, item.CurrentLocationId)) return [];
            blocked.UnionWith(locked);
        }
        var layout = new PutawayLayout(warehouse, locations,
            await repo.Query<ZoneStorageRule>().AsNoTracking().Where(r => r.ZoneLocation.WarehouseId == plan.WarehouseId).ToListAsync(ct), false, blocked);
        var physical = await repo.Query<SerialItem>().AsNoTracking().Include(s => s.Product)
            .Where(s => s.StockState == "IN_WAREHOUSE" && s.CurrentWarehouseId == plan.WarehouseId && s.CurrentLocationId != null).ToListAsync(ct);
        var occupants = physical.Select(s => new PutawayOccupant(s.SerialItemId, plan.WarehouseId, s.CurrentLocationId!.Value,
            s.ItemCondition,s.Product.LengthCm,s.Product.WidthCm,s.Product.HeightCm,s.Product.GrossWeightKg)).ToList();
        var probe = new SerialItem { SerialItemId = item.SerialItemId, ProductId = item.ProductId, Product = item.Product,
            ItemCondition = handling.ActualCondition, CurrentWarehouseId = plan.WarehouseId };
        var valid = locations.Where(l => l.LocationType is "TIER" or "FLOOR_SLOT")
            .Select(l => PutawayRules.Evaluate(probe, layout, l, occupants)).Where(e => e.Location is not null).Select(e => e.Location!).ToList();
        // Return to the registered original whenever it remains legal; fallback only when it is not.
        var original = valid.FirstOrDefault(l => l.Id == handling.OriginalLocationId);
        return original is not null ? [original] : valid.OrderBy(l => l.IsReserve).ThenBy(l => l.Path).ThenBy(l => l.Id).ToList();
    }

    private async Task<SupplySource> ConditionSourceAsync(SupplySource old, string condition, DateTime now, CancellationToken ct)
    {
        var source = await repo.Query<SupplySource>().SingleOrDefaultAsync(s => s.ProductId == old.ProductId && s.WarehouseId == old.WarehouseId
            && s.ItemCondition == condition && s.SupplyState == "PHYSICAL" && s.SourceType == old.SourceType
            && s.InboundId == old.InboundId && s.ReturnReceiptId == old.ReturnReceiptId && s.TransferReceiptId == old.TransferReceiptId && s.AdjustmentId == old.AdjustmentId, ct);
        if (source is not null) return source;
        source = new SupplySource { ProductId = old.ProductId, WarehouseId = old.WarehouseId, ItemCondition = condition,
            SourceType = old.SourceType, SupplyState = "PHYSICAL", InboundId = old.InboundId, ReturnReceiptId = old.ReturnReceiptId,
            TransferReceiptId = old.TransferReceiptId, AdjustmentId = old.AdjustmentId, AvailableFrom = old.AvailableFrom, CreatedAt = now };
        repo.Add(source); await repo.SaveAsync(ct); return source;
    }

    private async Task ReconcileAsync(long sourceId, DateTime now, CancellationToken ct)
    {
        var source = await repo.Query<SupplySource>().SingleAsync(s => s.SupplySourceId == sourceId, ct);
        var serials = await repo.Query<SerialItem>().AsNoTracking().Where(s => s.CurrentSupplySourceId == sourceId && s.StockState == "IN_WAREHOUSE").ToListAsync(ct);
        source.RemainingQuantity = serials.Count;
        source.EligibleQuantity = serials.Count(s => s.ItemCondition == "GOOD" && !s.IsQuarantined);
        var reservations = await repo.Query<Reservation>().Where(r => r.SupplySourceId == sourceId && r.Status == "ACTIVE")
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReservationId).ToListAsync(ct);
        var excess = Math.Max(0, reservations.Sum(r => r.ReservedQuantity-r.ConsumedQuantity-r.ReleasedQuantity)-source.EligibleQuantity);
        if (excess>0 && (await repo.Query<PickingClaim>().AnyAsync(c => c.SerialItem.CurrentSupplySourceId == sourceId, ct)
            || await repo.Query<DemandAllocation>().AnyAsync(a => a.SupplySourceId == sourceId
                && ((a.Outbound != null && a.Outbound.Status != "DONE" && a.Outbound.Status != "CANCELLED")
                    || (a.Dispatch != null && a.Dispatch.Status != "DONE" && a.Dispatch.Status != "CANCELLED")), ct)))
            throw new StocktakeWorkflowException("Nguồn đang có công việc xuất/chuyển kho. Cần xử lý công việc đó trước khi chuyển hàng hỏng.");
        await repo.SaveAsync(ct);
        await commitments.ReconcileWarehouseAsync(source.WarehouseId,
            "Kiểm kê ghi nhận thay đổi tình trạng hàng, một phần nguồn không còn đủ điều kiện.", ct);
    }

    private static void CheckVersion(byte[] expected, string supplied)
    {
        byte[] bytes; try { bytes=Convert.FromBase64String(supplied); } catch(FormatException) { throw new StocktakeWorkflowException("Phiên bản không hợp lệ."); }
        if(!expected.SequenceEqual(bytes)) throw new StocktakeWorkflowException("Dữ liệu vừa thay đổi. Hãy tải lại trang.",409);
    }
}
