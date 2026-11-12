using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;
using StocktakePlan = PRN222_EWMS_Web.Models.Stocktake;

namespace PRN222_EWMS_Web.Services.Stocktaking;

/// <summary>Manager review changes evidence/workflow only; official stock is changed by CEO approval/apply.</summary>
public partial class StocktakeReviewService(IStocktakeRepository repo) : IStocktakeReviewService
{
    public async Task<StocktakeReviewViewModel?> GetDetailsAsync(int actorId, long id, CancellationToken ct)
    {
        await RequireManagerAsync(actorId, ct);
        var plan = await repo.Query<StocktakePlan>().AsNoTracking().Include(p => p.Warehouse)
            .SingleOrDefaultAsync(p => p.StocktakeId == id, ct);
        if (plan is null) return null;
        var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == id)
            .OrderBy(r => r.RoundNumber).ToListAsync(ct);
        var requests = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == id)
            .OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.RecountRequestId).ToListAsync(ct);
        var observations = await repo.Query<StocktakeObservation>().AsNoTracking()
            .Include(o => o.Product).Include(o => o.ScannedByUser)
            .Where(o => o.StocktakeId == id).ToListAsync(ct);
        var productIds = observations.Where(o => o.ProductId.HasValue).Select(o => o.ProductId!.Value).Distinct().ToArray();
        var products = await repo.Query<Product>().AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .OrderBy(p => p.ProductName).ToListAsync(ct);
        var investigations = await repo.InvestigationsAsync(id, ct);
        var model = new StocktakeReviewViewModel
        {
            Plan = plan,
            Version = Convert.ToBase64String(plan.RowVersion),
            Assignments = await repo.Query<StocktakeAssignment>().AsNoTracking().Include(a => a.User)
                .Where(a => a.StocktakeId == id).ToListAsync(ct),
            Requests = requests,
            Rounds = rounds,
            Observations = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests),
            History = observations,
            Handling = await repo.HandlingAsync(id, ct),
            Investigations = investigations.Where(i => i.StocktakeId == id).ToList(),
            InvestigationContext = investigations.SingleOrDefault(i => i.InvestigationStocktakeId == id),
            Locations = await LocationsAsync(plan.WarehouseId, ct),
            Products = products,
            Staff = await repo.Query<User>().AsNoTracking().Where(u => u.WarehouseId == plan.WarehouseId
                && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF").OrderBy(u => u.FullName).ToListAsync(ct),
            Adjustments = await repo.Query<InventoryAdjustment>().AsNoTracking().Include(a => a.DecidedByUser).Where(a => a.StocktakeId == id)
                .OrderByDescending(a => a.CreatedAt).ToListAsync(ct)
        };
        var observationKeys = model.Observations.Select(o => o.ObservationId.ToString()).ToArray();
        model.NoSurplusDecisions = await repo.Query<AuditLog>().AsNoTracking().Include(a => a.ActorUser)
            .Where(a => a.TableName == NoSurplusAudit && observationKeys.Contains(a.EntityKey)).ToListAsync(ct);
        model.SourceDecisions = await repo.Query<AuditLog>().AsNoTracking().Include(a => a.ActorUser)
            .Where(a => a.TableName == SourceAudit && observationKeys.Contains(a.EntityKey)).ToListAsync(ct);
        var scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => s.StocktakeId == id).ToListAsync(ct);
        var planLocations = StocktakeScopeRules.PlanLocations(plan, scopes, model.Locations);
        var applied = await repo.Query<InventoryAdjustmentLine>().AsNoTracking()
            .Where(l => l.StocktakeId == id && l.IsActive && l.Adjustment.Status == "APPLIED")
            .Select(l => l.ScannedSerial).ToListAsync(ct);
        var canRequest = plan.Status is "REVIEWING" or "PENDING_APPROVAL"
            && rounds.All(r => r.Status != "OPEN")
            && model.Investigations.All(i => i.ClosedAt.HasValue)
            && model.Adjustments.All(a => a.Status is not ("DRAFT" or "SUBMITTED" or "APPROVED"));
        model.ConfirmedShortageIds = model.Investigations.SelectMany(i => i.StillMissingObservationIds).ToHashSet();
        if (canRequest && model.InvestigationContext is null && requests.All(r => r.Status is not ("REQUESTED" or "IN_PROGRESS"))
            && model.Handling.All(h => h.Status != "WAITING"))
            model.SearchableShortageIds = model.Observations.Where(o => CanSearchShortage(o, rounds, requests)
                && !model.ConfirmedShortageIds.Contains(o.ObservationId)
                && !applied.Contains(o.ScannedSerial, StringComparer.OrdinalIgnoreCase)).Select(o => o.ObservationId).ToHashSet();
        if (canRequest)
        {
            var proofCandidates = model.Investigations.Where(i => i.ClosedAt.HasValue && i.Outcome == "NEEDS_REVIEW").ToList();
            if (model.InvestigationContext is null)
                foreach (var observation in model.Observations.Where(NeedsSourceReview))
                    model.CompletedSources[observation.ObservationId] = await CompletedReceiptsAsync(observation, plan.WarehouseId, ct);
            var proofPlanIds = proofCandidates.Select(i => i.InvestigationStocktakeId).ToArray();
            var findings = await repo.Query<StocktakeObservation>().AsNoTracking()
                .Where(o => proofPlanIds.Contains(o.StocktakeId) && o.ActualQuantity == 1)
                .Select(o => new { o.StocktakeId, o.ScannedSerial }).ToListAsync(ct);
            foreach (var proof in proofCandidates)
            {
                var observation = model.Observations.FirstOrDefault(o => o.ObservationId == proof.ObservationId);
                if (observation is not null && !findings.Any(f => f.StocktakeId == proof.InvestigationStocktakeId
                    && string.Equals(f.ScannedSerial, observation.ScannedSerial, StringComparison.OrdinalIgnoreCase)))
                    model.NoSurplusProofIds.Add(proof.StocktakeInvestigationId);
            }
        }
        if (canRequest && model.InvestigationContext is null)
        foreach (var productId in model.Observations.Where(o => o.ProductId.HasValue
            && o.DifferenceType is not ("MATCHED" or "PENDING") && o.ReviewStatus != "RESOLVED")
            .Select(o => o.ProductId!.Value).Distinct())
        {
            var candidates = model.Locations.Where(l => l.Status == "ACTIVE"
                && StocktakeScopeRules.Descendants(l.LocationId, model.Locations).IsSubsetOf(planLocations)
                && model.Locations.Any(child => child.LocationType is "TIER" or "FLOOR_SLOT"
                    && StocktakeScopeRules.Descendants(l.LocationId, model.Locations).Contains(child.LocationId)))
                .Select(l => new StocktakeRecountRequest { ProductId = productId, ScopeKind = "LOCATION", LocationId = l.LocationId }).ToList();
            if (StocktakeScopeRules.InPlan(plan, planLocations, null))
                candidates.Add(new StocktakeRecountRequest { ProductId = productId, ScopeKind = "AWAITING_PUTAWAY" });
            foreach (var candidate in candidates)
            {
                var selected = model.Observations.Where(o => o.ReviewStatus != "RESOLVED" && (o.ExpectedQuantity == 1 || o.ActualQuantity == 1)
                    && StocktakeScopeRules.InRequest(candidate, o.ProductId,
                        o.ActualQuantity == 1 ? o.ActualLocationId : o.ExpectedLocationId, model.Locations)).ToList();
                if (!selected.Any(o => o.DifferenceType is not ("MATCHED" or "PENDING") && o.ReviewStatus != "RESOLVED")
                    || selected.Any(o => applied.Contains(o.ScannedSerial, StringComparer.OrdinalIgnoreCase))
                    || requests.Any(r => r.Status is "REQUESTED" or "IN_PROGRESS" && Overlaps(r, candidate, model.Locations))) continue;
                var staffIds = model.Staff.Where(staff => !CountedPreviously(staff.UserId, selected,
                    requests, rounds, model.Assignments, planLocations, model.Locations)).Select(s => s.UserId).ToList();
                model.RecountChoices.Add(new(productId, candidate.ScopeKind, candidate.LocationId, staffIds));
            }
        }
        return model;
    }

    public async Task CreateRecountAsync(int actorId, StocktakeRecountInput input, CancellationToken ct)
    {
        var reason = Reason(input.Reason);
        var warehouse = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouse, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var plan = await PlanAsync(input.Id, input.Version, token);
            RequireReview(plan);
            if (await repo.HasOpenInvestigationAsync(input.Id, token))
                throw new StocktakeWorkflowException("Cần kết luận cuộc đối soát toàn kho trước khi yêu cầu đếm lại.");
            if (await repo.InvestigationForAsync(input.Id, token) is not null)
                throw new StocktakeWorkflowException("Đối soát toàn kho dùng để xác minh; không mở đếm lại cục bộ từ đợt đối soát.");
            await RequireNoOpenRoundAsync(plan.StocktakeId, token);
            await RequireNoPendingAdjustmentAsync(plan.StocktakeId, token);
            var locations = await LocationsAsync(warehouse, token);
            var scopes = await repo.Query<StocktakeScope>().Where(s => s.StocktakeId == input.Id).ToListAsync(token);
            var planLocations = StocktakeScopeRules.PlanLocations(plan, scopes, locations);
            if (input.ScopeKind is not ("LOCATION" or "AWAITING_PUTAWAY"))
                throw new StocktakeWorkflowException("Phạm vi đếm lại không hợp lệ.");
            if (input.ScopeKind == "LOCATION")
            {
                if (!input.LocationId.HasValue || !locations.Any(l => l.LocationId == input.LocationId && l.Status == "ACTIVE"))
                    throw new StocktakeWorkflowException("Chọn khu vực đang hoạt động trong kho kiểm kê.");
                var descendants = StocktakeScopeRules.Descendants(input.LocationId.Value, locations);
                var leaves = locations.Where(l => descendants.Contains(l.LocationId)
                    && l.LocationType is "TIER" or "FLOOR_SLOT").ToList();
                if (leaves.Count == 0 || !descendants.IsSubsetOf(planLocations))
                    throw new StocktakeWorkflowException("Khu vực đếm lại phải nằm hoàn toàn trong phạm vi kế hoạch ban đầu.");
            }
            else if (input.LocationId is not null || !StocktakeScopeRules.InPlan(plan, planLocations, null))
                throw new StocktakeWorkflowException("Kế hoạch không bao gồm hàng chờ xếp kệ.");

            var staff = await repo.Query<User>().SingleOrDefaultAsync(u => u.UserId == input.AssignedToUserId
                && u.Status == "ACTIVE" && u.WarehouseId == warehouse && u.Role.RoleName == "WAREHOUSE_STAFF", token);
            if (staff is null) throw new StocktakeWorkflowException("Người đếm lại phải là nhân viên kho đang hoạt động trong cùng kho.");
            var requests = await repo.Query<StocktakeRecountRequest>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            var rounds = await repo.Query<StocktakeRound>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            var observations = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == input.Id).ToListAsync(token);
            var request = new StocktakeRecountRequest
            {
                StocktakeId = input.Id, ProductId = input.ProductId, ScopeKind = input.ScopeKind,
                LocationId = input.LocationId, RequestedByUserId = actorId, AssignedToUserId = staff.UserId,
                Reason = reason, Status = "REQUESTED", RequestedAt = DateTime.UtcNow
            };
            var effective = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests);
            // A serial already found outside this area is not missing merely because
            // a local recount cannot see it. Recount its last observed area instead.
            var selected = effective.Where(o => o.ReviewStatus != "RESOLVED" && (o.ExpectedQuantity == 1 || o.ActualQuantity == 1)
                && StocktakeScopeRules.InRequest(request, o.ProductId,
                    o.ActualQuantity == 1 ? o.ActualLocationId : o.ExpectedLocationId, locations))
                .ToList();
            if (!selected.Any(o => o.DifferenceType is not ("MATCHED" or "PENDING") && o.ReviewStatus != "RESOLVED"))
                throw new StocktakeWorkflowException("Chưa có chênh lệch cần đếm lại của sản phẩm trong khu vực đã chọn.");
            var appliedSerials = await repo.Query<InventoryAdjustmentLine>().Where(l => l.StocktakeId == input.Id
                && l.IsActive && l.Adjustment.Status == "APPLIED").Select(l => l.ScannedSerial).ToListAsync(token);
            if (selected.Any(o => appliedSerials.Contains(o.ScannedSerial, StringComparer.OrdinalIgnoreCase)))
                throw new StocktakeWorkflowException("Phạm vi có serial đã áp dụng điều chỉnh. Không đếm lại trên mốc tồn cũ; cần một đợt kiểm kê mới.");
            if (requests.Where(r => r.Status is "REQUESTED" or "IN_PROGRESS").Any(r => Overlaps(r, request, locations)))
                throw new StocktakeWorkflowException("Đã có yêu cầu đếm lại chưa hoàn tất cho sản phẩm tại phạm vi chồng nhau.");

            var assignments = await repo.Query<StocktakeAssignment>().Where(a => a.StocktakeId == input.Id && a.Status != "CANCELLED").ToListAsync(token);
            if (CountedPreviously(staff.UserId, selected, requests, rounds, assignments, planLocations, locations))
                throw new StocktakeWorkflowException("Phải giao người khác với người đã đếm phần được chọn ở lần trước.");
            var sourceNumber = selected.Max(o => o.RoundNumber);
            request.SourceRoundId = rounds.Single(r => r.RoundNumber == sourceNumber && r.Status == "CLOSED").RoundId;
            foreach (var observation in selected.Where(o => o.DifferenceType != "MATCHED")) observation.ReviewStatus = "RECOUNT_REQUIRED";
            plan.Status = "REVIEWING";
            repo.TouchPlan(plan);
            repo.Add(request);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    public async Task CancelRecountAsync(int actorId, StocktakeActionInput input, CancellationToken ct)
    {
        var warehouse = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouse, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var plan = await PlanAsync(input.Id, input.Version, token);
            if (plan.Status is "COMPLETED" or "CANCELLED") throw new StocktakeWorkflowException("Đợt kiểm kê đã đóng.");
            var request = await repo.Query<StocktakeRecountRequest>().SingleOrDefaultAsync(r => r.StocktakeId == input.Id
                && r.RecountRequestId == input.RecountRequestId, token)
                ?? throw new StocktakeWorkflowException("Không tìm thấy yêu cầu đếm lại.", 404);
            if (request.Status == "CANCELLED") return true;
            if (request.Status != "REQUESTED") throw new StocktakeWorkflowException("Chỉ hủy được yêu cầu chưa bắt đầu.");
            request.Status = "CANCELLED";
            request.CancelledAt = DateTime.UtcNow;
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    public async Task SubmitAdjustmentAsync(int actorId, StocktakeAdjustmentInput input, CancellationToken ct)
    {
        var reason = Reason(input.Reason);
        var ids = input.ObservationIds.Distinct().ToHashSet();
        if (ids.Count == 0) throw new StocktakeWorkflowException("Chọn ít nhất một chênh lệch cần điều chỉnh.");
        var warehouse = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouse, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var plan = await PlanAsync(input.Id, input.Version, token);
            RequireReview(plan);
            if (await repo.HasInvestigationRestrictionAsync(input.Id, token))
                throw new StocktakeWorkflowException("Đợt đối soát chỉ lưu bằng chứng; cần kết luận và gửi đề nghị từ đợt kiểm kê gốc.");
            await RequireNoOpenRoundAsync(plan.StocktakeId, token);
            await RequireNoPendingAdjustmentAsync(plan.StocktakeId, token);
            var requests = await repo.Query<StocktakeRecountRequest>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            if (requests.Any(r => r.Status is "REQUESTED" or "IN_PROGRESS"))
                throw new StocktakeWorkflowException("Cần hoàn tất hoặc hủy yêu cầu đếm lại đang chờ trước khi gửi điều chỉnh.");
            var rounds = await repo.Query<StocktakeRound>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            var observations = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == input.Id).ToListAsync(token);
            var selected = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests)
                .Where(o => ids.Contains(o.ObservationId)).ToList();
            if (selected.Count != ids.Count || selected.Any(o => o.DifferenceType != "MISSING"
                || o.ExpectedQuantity != 1 || o.ActualQuantity != 0 || o.ReviewStatus == "RESOLVED"
                || o.ProductId is null || o.SerialItemId is null))
                throw new StocktakeWorkflowException("Chỉ gửi CEO đề nghị giảm tồn cho serial thiếu đã xác minh.");

            var confirmed = (await repo.InvestigationsAsync(plan.StocktakeId, token))
                .SelectMany(i => i.StillMissingObservationIds).ToHashSet();
            if (!confirmed.IsSupersetOf(ids))
                throw new StocktakeWorkflowException("Chỉ gửi CEO sau khi Manager chốt vẫn thiếu qua bước tìm serial toàn kho.");

            var oldLines = await repo.Query<InventoryAdjustmentLine>().Include(l => l.Adjustment)
                .Where(l => l.StocktakeId == input.Id && l.IsActive).ToListAsync(token);
            // A rejected/cancelled proposal must not reserve the serial forever.
            foreach (var old in oldLines.Where(l => l.Adjustment.Status is "REJECTED" or "CANCELLED")) old.IsActive = false;
            if (selected.Any(o => oldLines.Any(l => l.IsActive && string.Equals(l.ScannedSerial, o.ScannedSerial, StringComparison.OrdinalIgnoreCase))))
                throw new StocktakeWorkflowException("Có serial đã được đưa vào yêu cầu điều chỉnh còn hiệu lực hoặc đã áp dụng.");
            await repo.SaveAsync(token);
            var now = DateTime.UtcNow;
            var adjustment = new InventoryAdjustment
            {
                StocktakeId = input.Id, SubmittedByUserId = actorId,
                AdjustmentCode = $"IA-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..40],
                Status = "DRAFT", Reason = reason, CreatedAt = now
            };
            repo.Add(adjustment);
            await repo.SaveAsync(token);
            foreach (var observation in selected)
            {
                repo.Add(new InventoryAdjustmentLine
                {
                    AdjustmentId = adjustment.AdjustmentId, ObservationId = observation.ObservationId,
                    StocktakeId = input.Id, ScannedSerial = observation.ScannedSerial,
                    SerialItemId = observation.SerialItemId, ProductId = observation.ProductId,
                    AdjustmentType = "REMOVE", QuantityDelta = -1,
                    IsActive = true, Note = reason
                });
                observation.ReviewStatus = "REVIEWED";
            }
            await repo.SaveAsync(token);
            adjustment.Status = "SUBMITTED";
            adjustment.SubmittedAt = now;
            plan.Status = "PENDING_APPROVAL";
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    public async Task CompleteAsync(int actorId, StocktakeActionInput input, CancellationToken ct)
    {
        var warehouse = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouse, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var plan = await PlanAsync(input.Id, input.Version, token);
            if (await repo.HasInvestigationRestrictionAsync(input.Id, token))
                throw new StocktakeWorkflowException("Cần kết luận đối soát toàn kho bằng thao tác kết luận riêng; không hoàn tất thay cho đối soát.");
            if ((await repo.HandlingAsync(input.Id, token)).Any(h => h.Status == "WAITING"))
                throw new StocktakeWorkflowException("Còn hàng chưa xác nhận chuyển đến vị trí hợp lệ.");
            if (plan.Status == "COMPLETED") return true;
            if (plan.Status is not ("REVIEWING" or "PENDING_APPROVAL"))
                throw new StocktakeWorkflowException("Chỉ hoàn tất sau khi kết thúc kiểm đếm và rà soát.");
            await RequireNoOpenRoundAsync(input.Id, token);
            await RequireNoPendingAdjustmentAsync(input.Id, token);
            if (await repo.Query<StocktakeAssignment>().AnyAsync(a => a.StocktakeId == input.Id
                    && a.Status != "DONE" && a.Status != "CANCELLED", token))
                throw new StocktakeWorkflowException("Còn nhân viên chưa gửi kết quả kiểm kê.");
            var requests = await repo.Query<StocktakeRecountRequest>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            if (requests.Any(r => r.Status is "REQUESTED" or "IN_PROGRESS"))
                throw new StocktakeWorkflowException("Còn yêu cầu đếm lại chưa hoàn tất.");
            var rounds = await repo.Query<StocktakeRound>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            if (!rounds.Any(r => r.RoundNumber == 1 && r.Status == "CLOSED"))
                throw new StocktakeWorkflowException("Chưa hoàn tất vòng kiểm kê đầu tiên.");
            var observations = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == input.Id).ToListAsync(token);
            var effective = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests);
            var applied = await repo.Query<InventoryAdjustmentLine>().Where(l => l.StocktakeId == input.Id
                && l.IsActive && l.Adjustment.Status == "APPLIED").Select(l => l.ScannedSerial).ToListAsync(token);
            var appliedSerials = applied.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (effective.Any(o => o.DifferenceType == "PENDING" || (o.DifferenceType != "MATCHED"
                && o.ReviewStatus != "RESOLVED" && !appliedSerials.Contains(o.ScannedSerial))))
                throw new StocktakeWorkflowException("Còn chênh lệch chưa được giải quyết hoặc chưa áp dụng điều chỉnh được CEO duyệt.");
            foreach (var observation in effective) observation.ReviewStatus = "RESOLVED";
            plan.Status = "COMPLETED";
            plan.ClosedAt = DateTime.UtcNow;
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    private async Task RequireManagerAsync(int actorId, CancellationToken ct)
    {
        if (!await repo.Query<User>().AsNoTracking().AnyAsync(u => u.UserId == actorId && u.Status == "ACTIVE"
                && u.Role.RoleName == "MANAGER", ct))
            throw new StocktakeWorkflowException("Tài khoản không còn quyền Manager đang hoạt động.", 403);
    }

    private async Task<int> WarehouseAsync(int actorId, long id, CancellationToken ct)
    {
        await RequireManagerAsync(actorId, ct);
        return await repo.Query<StocktakePlan>().AsNoTracking().Where(p => p.StocktakeId == id)
            .Select(p => (int?)p.WarehouseId).SingleOrDefaultAsync(ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy đợt kiểm kê.", 404);
    }

    private async Task<StocktakePlan> PlanAsync(long id, string version, CancellationToken ct)
    {
        var plan = await repo.Query<StocktakePlan>().SingleOrDefaultAsync(p => p.StocktakeId == id, ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy đợt kiểm kê.", 404);
        if (string.IsNullOrWhiteSpace(version) || Convert.ToBase64String(plan.RowVersion) != version)
            throw new StocktakeWorkflowException("Đợt kiểm kê vừa thay đổi. Hãy tải lại trang trước khi thao tác.", 409);
        return plan;
    }

    private Task<List<StorageLocation>> LocationsAsync(int warehouseId, CancellationToken ct) =>
        repo.Query<StorageLocation>().AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);

    private async Task RequireNoOpenRoundAsync(long id, CancellationToken ct)
    {
        if (await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == id && r.Status == "OPEN", ct))
            throw new StocktakeWorkflowException("Cần kết thúc vòng đếm đang mở trước khi rà soát.");
    }

    private async Task RequireNoPendingAdjustmentAsync(long id, CancellationToken ct)
    {
        if (await repo.Query<InventoryAdjustment>().AnyAsync(a => a.StocktakeId == id
            && (a.Status == "DRAFT" || a.Status == "SUBMITTED" || a.Status == "APPROVED"), ct))
            throw new StocktakeWorkflowException("Có yêu cầu điều chỉnh đang xử lý hoặc chưa áp dụng. Chưa thể tiếp tục thao tác này.");
    }

    private static void RequireReview(StocktakePlan plan)
    {
        if (plan.Status is not ("REVIEWING" or "PENDING_APPROVAL"))
            throw new StocktakeWorkflowException("Chỉ được thao tác sau khi nhân viên đã gửi đủ kết quả và kết thúc vòng đếm.");
    }

    private static string Reason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length is < 1 or > 1000) throw new StocktakeWorkflowException("Nhập lý do từ 1 đến 1.000 ký tự.");
        return reason;
    }

    private static bool CountedPreviously(int staffId, IEnumerable<StocktakeObservation> selected,
        IEnumerable<StocktakeRecountRequest> requests, IEnumerable<StocktakeRound> rounds,
        IEnumerable<StocktakeAssignment> assignments, HashSet<int> planLocations, IReadOnlyCollection<StorageLocation> locations) =>
        selected.Any(o => o.ScannedByUserId == staffId
            || requests.Any(r => r.Status == "SUBMITTED" && r.AssignedToUserId == staffId
                && rounds.Any(round => round.RoundId == r.ResultRoundId && round.RoundNumber == o.RoundNumber))
            || (o.RoundNumber == 1 && assignments.Any(a => a.Status != "CANCELLED" && a.UserId == staffId
                && StocktakeScopeRules.InAssignment(a, o.ExpectedQuantity == 1 ? o.ExpectedLocationId : o.ActualLocationId, planLocations, locations))));

    private static bool Overlaps(StocktakeRecountRequest first, StocktakeRecountRequest second,
        IReadOnlyCollection<StorageLocation> locations)
    {
        if (first.ProductId != second.ProductId || first.ScopeKind != second.ScopeKind) return false;
        if (first.ScopeKind == "AWAITING_PUTAWAY") return true;
        return first.LocationId is int a && second.LocationId is int b
            && StocktakeScopeRules.Descendants(a, locations).Overlaps(StocktakeScopeRules.Descendants(b, locations));
    }

}
