using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;
using StocktakePlan = PRN222_EWMS_Web.Models.Stocktake;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public partial class StocktakeReviewService
{
    public async Task<long> CreateInvestigationAsync(int actorId, StocktakeInvestigationInput input, CancellationToken ct)
    {
        var reason = Reason(input.Reason);
        var warehouseId = await WarehouseAsync(actorId, input.Id, ct);
        return await repo.TransactionAsync(actorId, warehouseId, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var parent = await PlanAsync(input.Id, input.Version, token);
            RequireReview(parent);
            await RequireNoOpenRoundAsync(parent.StocktakeId, token);
            await RequireNoPendingAdjustmentAsync(parent.StocktakeId, token);
            if ((await repo.HandlingAsync(parent.StocktakeId, token)).Any(h => h.Status == "WAITING")
                || await repo.Query<StocktakeAssignment>().AnyAsync(a => a.StocktakeId == parent.StocktakeId && a.Status != "DONE" && a.Status != "CANCELLED", token))
                throw new StocktakeWorkflowException("Cần gửi đủ kết quả và xử lý hàng chờ chuyển trước khi mở đối soát toàn kho.");
            if (await repo.InvestigationForAsync(parent.StocktakeId, token) is not null)
                throw new StocktakeWorkflowException("Không mở thêm cuộc đối soát từ một đợt đối soát. Hãy kết luận cuộc đối soát hiện tại.");
            if (await repo.Query<StocktakeRecountRequest>().AnyAsync(r => r.StocktakeId == parent.StocktakeId
                    && (r.Status == "REQUESTED" || r.Status == "IN_PROGRESS"), token))
                throw new StocktakeWorkflowException("Cần hoàn tất hoặc hủy yêu cầu đếm lại đang chờ trước khi đối soát toàn kho.");
            // One whole-warehouse investigation at a time. Its child plan is the ordinary stocktake lock contract.
            var investigationIds = (await repo.InvestigationsAsync(null, token)).Where(i => i.ClosedAt == null)
                .Select(i => i.InvestigationStocktakeId).ToList();
            var openInvestigationCode = await repo.Query<StocktakePlan>()
                .Where(s => s.WarehouseId == warehouseId && investigationIds.Contains(s.StocktakeId)
                    && s.Status != "CANCELLED" && s.Status != "COMPLETED")
                .OrderBy(s => s.StocktakeId).Select(s => s.StocktakeCode).FirstOrDefaultAsync(token);
            if (openInvestigationCode is not null)
                throw new StocktakeWorkflowException($"Kho đã có nhiệm vụ đối soát {openInvestigationCode} chưa kết luận.");
            var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == parent.StocktakeId).ToListAsync(token);
            var recounts = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == parent.StocktakeId).ToListAsync(token);
            var observations = await repo.Query<StocktakeObservation>().AsNoTracking().Where(o => o.StocktakeId == parent.StocktakeId).ToListAsync(token);
            var effective = StocktakeScopeRules.EffectiveObservations(observations, rounds, recounts);
            var shortageIds = input.ShortageObservationIds.Distinct().ToList();
            var observationId = input.ObservationId;
            if (shortageIds.Count > 0)
            {
                var confirmed = (await repo.InvestigationsAsync(parent.StocktakeId, token))
                    .SelectMany(i => i.StillMissingObservationIds).ToHashSet();
                var applied = await repo.Query<InventoryAdjustmentLine>().Where(l => l.StocktakeId == parent.StocktakeId
                    && l.IsActive && l.Adjustment.Status == "APPLIED").Select(l => l.ScannedSerial).ToListAsync(token);
                var eligible = effective.Where(o => CanSearchShortage(o)
                    && !confirmed.Contains(o.ObservationId) && !applied.Contains(o.ScannedSerial, StringComparer.OrdinalIgnoreCase))
                    .Where(o => shortageIds.Contains(o.ObservationId)).ToList();
                if (eligible.Count != shortageIds.Count)
                    throw new StocktakeWorkflowException("Chỉ tìm toàn kho các serial đang báo thiếu trong kết quả kiểm kê đã gửi.");
                var conflict = await repo.Query<StocktakePlan>().AsNoTracking()
                    .Where(s => s.WarehouseId == warehouseId && s.StocktakeId != parent.StocktakeId
                        && StocktakeScopeRules.LockedStatuses.Contains(s.Status))
                    .OrderBy(s => s.StocktakeId).Select(s => s.StocktakeCode).FirstOrDefaultAsync(token);
                if (conflict is not null)
                    throw new StocktakeWorkflowException($"Kho đang có đợt kiểm kê khóa phạm vi {conflict}. Hãy xử lý đợt đó trước khi giao tìm toàn kho.");
                var locations = await LocationsAsync(warehouseId, token);
                var scopes = await repo.Query<StocktakeScope>().AsNoTracking()
                    .Where(s => s.StocktakeId == parent.StocktakeId).ToListAsync(token);
                var assignments = await repo.Query<StocktakeAssignment>().AsNoTracking()
                    .Where(a => a.StocktakeId == parent.StocktakeId).ToListAsync(token);
                var targetSerials = eligible.Select(o => o.ScannedSerial).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var initialCounts = observations.Where(o => o.RoundNumber == 1 && targetSerials.Contains(o.ScannedSerial));
                if (CountedPreviously(input.AssignedToUserId, initialCounts, recounts, rounds, assignments,
                    StocktakeScopeRules.PlanLocations(parent, scopes, locations), locations))
                    throw new StocktakeWorkflowException("Hãy giao tìm cho nhân viên khác người phụ trách đếm lần đầu của các serial đã chọn.");
                var serialIds = eligible.Select(o => o.SerialItemId!.Value).ToArray();
                var stock = await repo.Query<SerialItem>().AsNoTracking()
                    .Where(s => serialIds.Contains(s.SerialItemId)).ToDictionaryAsync(s => s.SerialItemId, token);
                if (eligible.Any(o => !stock.TryGetValue(o.SerialItemId!.Value, out var item)
                    || !StocktakeReceivingEvidence.IsInWarehouse(item, warehouseId) || item.ProductId != o.ProductId
                    || item.CurrentLocationId != o.ExpectedLocationId || item.ItemCondition != o.ExpectedCondition))
                    throw new StocktakeWorkflowException("Dữ liệu sổ kho của serial cần tìm đã thay đổi. Kiểm tra lại trước khi giao tìm.");
                observationId = shortageIds[0];
            }
            else
            {
                var observation = effective.SingleOrDefault(o => o.ObservationId == observationId);
                if (observation is null || observation.ExpectedQuantity != 0 || observation.ActualQuantity != 1
                    || observation.SerialItemId.HasValue || observation.ProductId is null || observation.ReviewStatus == "RESOLVED")
                    throw new StocktakeWorkflowException("Chọn serial cần đối soát.");
                await StocktakeUnknownSerialRules.EnsureNoReceivingContextAsync(repo, observation.ScannedSerial, token);
            }
            var staff = await repo.Query<User>().AsNoTracking().SingleOrDefaultAsync(u => u.UserId == input.AssignedToUserId
                && u.Status == "ACTIVE" && u.WarehouseId == warehouseId && u.Role.RoleName == "WAREHOUSE_STAFF", token);
            if (staff is null) throw new StocktakeWorkflowException("Chọn nhân viên kho đang hoạt động trong cùng kho để đối soát.");
            var now = DateTime.UtcNow;
            var child = new StocktakePlan
            {
                WarehouseId = warehouseId, CreatedByUserId = actorId,
                StocktakeCode = await codes.NextAsync(DocumentType.StocktakeInvestigation, token),
                ScopeType = "WHOLE_WAREHOUSE", IncludeAwaitingPutaway = true,
                Status = "PLANNED", CreatedAt = now, PlannedAt = now, Note = reason
            };
            repo.Add(child); await repo.SaveAsync(token);
            repo.Add(new StocktakeAssignment { StocktakeId = child.StocktakeId, UserId = staff.UserId,
                WarehouseId = warehouseId, ScopeKind = "ALL_SCOPE", Status = "ASSIGNED", AssignedAt = now });
            repo.Record(new StocktakeInvestigation { StocktakeInvestigationId = -child.StocktakeId,
                StocktakeId = parent.StocktakeId, ObservationId = observationId, ShortageObservationIds = shortageIds,
                InvestigationStocktakeId = child.StocktakeId, RequestedByUserId = actorId, RequestedAt = now }, actorId, isNew: true);
            parent.Status = "REVIEWING";
            repo.TouchPlan(parent); await repo.SaveAsync(token);
            return child.StocktakeId;
        }, ct);
    }

    public async Task FinishInvestigationAsync(int actorId, StocktakeInvestigationConclusionInput input, CancellationToken ct)
    {
        var conclusion = Reason(input.Conclusion);
        var warehouseId = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouseId, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var child = await PlanAsync(input.Id, input.Version, token);
            var investigation = await repo.InvestigationForAsync(child.StocktakeId, token)
                ?? throw new StocktakeWorkflowException("Không tìm thấy nhiệm vụ đối soát.", 404);
            var shortageSearch = investigation.ShortageObservationIds.Count > 0;
            if (input.Outcome != (shortageSearch ? "SHORTAGE_SEARCH_COMPLETED" : "NEEDS_REVIEW"))
                throw new StocktakeWorkflowException("Kết luận không phù hợp với nhiệm vụ đối soát.");
            if (investigation.ClosedAt.HasValue) throw new StocktakeWorkflowException("Cuộc đối soát đã được kết luận.", 409);
            if (child.Status != "REVIEWING") throw new StocktakeWorkflowException("Cần gửi xong toàn bộ kết quả đối soát trước khi kết luận.");
            if (child.ScopeType != "WHOLE_WAREHOUSE" || !child.IncludeAwaitingPutaway)
                throw new StocktakeWorkflowException("Phạm vi đối soát phải là toàn kho và bao gồm hàng chờ xếp.");
            await RequireNoOpenRoundAsync(child.StocktakeId, token);
            await RequireNoPendingAdjustmentAsync(child.StocktakeId, token);
            if (await repo.Query<StocktakeAssignment>().AnyAsync(a => a.StocktakeId == child.StocktakeId && a.Status != "DONE" && a.Status != "CANCELLED", token)
                || await repo.Query<StocktakeRecountRequest>().AnyAsync(r => r.StocktakeId == child.StocktakeId && (r.Status == "REQUESTED" || r.Status == "IN_PROGRESS"), token))
                throw new StocktakeWorkflowException("Còn phần đếm hoặc yêu cầu đếm lại chưa gửi kết quả.");
            if (!await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == child.StocktakeId && r.RoundNumber == 1 && r.Status == "CLOSED", token))
                throw new StocktakeWorkflowException("Chưa hoàn tất vòng đếm toàn kho.");
            var parent = await repo.Query<StocktakePlan>().SingleAsync(s => s.StocktakeId == investigation.StocktakeId, token);
            if (parent.WarehouseId != warehouseId || parent.Status is not ("REVIEWING" or "PENDING_APPROVAL"))
                throw new StocktakeWorkflowException("Đợt kiểm kê gốc không còn ở trạng thái rà soát.", 409);
            var now = DateTime.UtcNow;
            if (shortageSearch)
            {
                var sourceRows = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == parent.StocktakeId
                    && investigation.ShortageObservationIds.Contains(o.ObservationId)).ToListAsync(token);
                var findings = await repo.Query<StocktakeObservation>().AsNoTracking()
                    .Where(o => o.StocktakeId == child.StocktakeId && o.RoundNumber == 1).ToListAsync(token);
                if (sourceRows.Count != investigation.ShortageObservationIds.Count
                    || (await repo.HandlingAsync(child.StocktakeId, token)).Any(h => h.Status == "WAITING"))
                    throw new StocktakeWorkflowException("Cần xử lý xong các ghi nhận trước khi chốt tìm kiếm.");
                foreach (var source in sourceRows)
                {
                    var finding = findings.SingleOrDefault(o => o.SerialItemId == source.SerialItemId && o.ProductId == source.ProductId);
                    if (finding is null || source.ReviewStatus == "RESOLVED")
                        throw new StocktakeWorkflowException("Dữ liệu serial đã thay đổi. Cần kiểm tra lại trước khi chốt.");
                    if (finding.ActualQuantity == 1 && finding.ScannedAt.HasValue)
                    {
                        var foundAtExpectedLocationWithConditionNote = finding.ExpectedCondition == "DAMAGED"
                            && finding.ActualCondition == "GOOD" && finding.ExpectedLocationId == finding.ActualLocationId;
                        if (finding.DifferenceType != "MATCHED" && finding.ReviewStatus != "RESOLVED"
                            && !foundAtExpectedLocationWithConditionNote)
                            throw new StocktakeWorkflowException($"Cần xử lý chênh lệch của {source.ScannedSerial} trước khi chốt.");
                        source.ReviewStatus = "RESOLVED";
                    }
                    else
                    {
                        investigation.StillMissingObservationIds.Add(source.ObservationId);
                    }
                }
            }
            investigation.Outcome = input.Outcome; investigation.Conclusion = conclusion;
            investigation.ClosedAt = now; investigation.ClosedByUserId = actorId;
            repo.Record(investigation, actorId, isNew: false);
            // A whole-warehouse investigation collects evidence, not a second stock reconciliation.
            // Preserve all count differences; only its counting lock is released here.
            child.Status = "COMPLETED"; child.ClosedAt = now;
            repo.TouchPlan(child); repo.TouchPlan(parent); await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    private static bool CanSearchShortage(StocktakeObservation row) =>
        row.DifferenceType == "MISSING" && row.ExpectedQuantity == 1 && row.ActualQuantity == 0
        && row.ReviewStatus != "RESOLVED" && row.SerialItemId.HasValue && row.ProductId.HasValue;
}

internal static class StocktakeUnknownSerialRules
{
    public static async Task EnsureNoReceivingContextAsync(IStocktakeRepository repo, string serial, CancellationToken ct)
    {
        if (await repo.Query<SerialItem>().AsNoTracking().AnyAsync(s => s.SerialNumber == serial, ct))
            throw new StocktakeWorkflowException($"Serial {serial} đã tồn tại trong hệ thống. Cần kiểm tra kho/trạng thái và nguồn gốc, không coi là serial mới.");
        if (await repo.Query<InboundItem>().AsNoTracking().AnyAsync(i => i.ScannedSerial == serial && i.IsActive && i.Inbound.Status == "DRAFT", ct))
            throw new StocktakeWorkflowException($"Serial {serial} đang có trong phiếu nhập nháp. Cần xử lý chứng từ nguồn, không điều chỉnh tăng tồn hai lần.");
        if (await repo.Query<ReturnReceiptItem>().AsNoTracking().AnyAsync(i => i.IsActive && i.ReturnReceipt.Status == "DRAFT" && i.OutboundItem.SerialItem.SerialNumber == serial, ct)
            || await repo.Query<TransferReceiptItem>().AsNoTracking().AnyAsync(i => i.IsActive && i.TransferReceipt.Status == "DRAFT" && i.DispatchItem.SerialItem.SerialNumber == serial, ct))
            throw new StocktakeWorkflowException($"Serial {serial} có chứng từ nhận trả/nhận chuyển nháp. Cần xác minh chứng từ nguồn trước.");
    }
}
