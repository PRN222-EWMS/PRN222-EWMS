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
            if (await repo.Query<StocktakePlan>().AnyAsync(s => s.WarehouseId == warehouseId
                    && investigationIds.Contains(s.StocktakeId) && s.Status != "CANCELLED" && s.Status != "COMPLETED", token))
                throw new StocktakeWorkflowException("Kho đã có yêu cầu đối soát toàn kho chưa kết luận.");
            var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == parent.StocktakeId).ToListAsync(token);
            var recounts = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == parent.StocktakeId).ToListAsync(token);
            var observations = await repo.Query<StocktakeObservation>().AsNoTracking().Where(o => o.StocktakeId == parent.StocktakeId).ToListAsync(token);
            var observation = StocktakeScopeRules.EffectiveObservations(observations, rounds, recounts)
                .SingleOrDefault(o => o.ObservationId == input.ObservationId);
            if (observation is null || observation.ExpectedQuantity != 0 || observation.ActualQuantity != 1
                || observation.SerialItemId.HasValue || observation.ProductId is null || observation.ReviewStatus == "RESOLVED")
                throw new StocktakeWorkflowException("Chỉ đối soát serial lạ hiện hành đã ghi nhận sản phẩm và chưa được giải quyết.");
            await StocktakeUnknownSerialRules.EnsureNoReceivingContextAsync(repo, observation.ScannedSerial, token);
            var staff = await repo.Query<User>().AsNoTracking().SingleOrDefaultAsync(u => u.UserId == input.AssignedToUserId
                && u.Status == "ACTIVE" && u.WarehouseId == warehouseId && u.Role.RoleName == "WAREHOUSE_STAFF", token);
            if (staff is null) throw new StocktakeWorkflowException("Chọn nhân viên kho đang hoạt động trong cùng kho để đối soát.");
            var now = DateTime.UtcNow;
            var child = new StocktakePlan
            {
                WarehouseId = warehouseId, CreatedByUserId = actorId,
                StocktakeCode = $"ST-INV-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..40],
                ScopeType = "WHOLE_WAREHOUSE", IncludeAwaitingPutaway = true,
                Status = "PLANNED", CreatedAt = now, PlannedAt = now, Note = reason
            };
            repo.Add(child); await repo.SaveAsync(token);
            repo.Add(new StocktakeAssignment { StocktakeId = child.StocktakeId, UserId = staff.UserId,
                WarehouseId = warehouseId, ScopeKind = "ALL_SCOPE", Status = "ASSIGNED", AssignedAt = now });
            repo.Record(new StocktakeInvestigation { StocktakeInvestigationId = -child.StocktakeId,
                StocktakeId = parent.StocktakeId, ObservationId = observation.ObservationId,
                InvestigationStocktakeId = child.StocktakeId, RequestedByUserId = actorId, RequestedAt = now }, actorId, isNew: true);
            parent.Status = "REVIEWING";
            repo.TouchPlan(parent); await repo.SaveAsync(token);
            return child.StocktakeId;
        }, ct);
    }

    public async Task FinishInvestigationAsync(int actorId, StocktakeInvestigationConclusionInput input, CancellationToken ct)
    {
        var conclusion = Reason(input.Conclusion);
        if (input.Outcome != "NEEDS_REVIEW")
            throw new StocktakeWorkflowException("Chọn kết luận cần tiếp tục xác minh nguồn gốc.");
        var warehouseId = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouseId, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var child = await PlanAsync(input.Id, input.Version, token);
            var investigation = await repo.InvestigationForAsync(child.StocktakeId, token)
                ?? throw new StocktakeWorkflowException("Đợt này không phải cuộc đối soát serial lạ.", 404);
            if (investigation.ClosedAt.HasValue) throw new StocktakeWorkflowException("Cuộc đối soát đã được kết luận.", 409);
            if (child.Status != "REVIEWING") throw new StocktakeWorkflowException("Cần gửi xong toàn bộ kết quả đối soát trước khi kết luận.");
            if (child.ScopeType != "WHOLE_WAREHOUSE" || !child.IncludeAwaitingPutaway)
                throw new StocktakeWorkflowException("Phạm vi đối soát không còn là toàn kho, không thể dùng làm căn cứ xác nhận thừa.");
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
