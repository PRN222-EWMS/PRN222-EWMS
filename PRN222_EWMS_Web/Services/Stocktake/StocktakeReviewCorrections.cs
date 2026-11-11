using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;
using Plan = PRN222_EWMS_Web.Models.Stocktake;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public partial class StocktakeReviewService
{
    private const string NoSurplusAudit = "StocktakeNoSurplus";
    private const string SourceAudit = "StocktakeSourceResolution";

    public async Task ConfirmSourceAsync(int actorId, StocktakeSourceResolutionInput input, CancellationToken ct)
    {
        var reason = Reason(input.Reason);
        var warehouse = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouse, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var plan = await PlanAsync(input.Id, input.Version, token);
            RequireReview(plan);
            await RequireNoOpenRoundAsync(input.Id, token);
            await RequireNoPendingAdjustmentAsync(input.Id, token);
            if (await repo.HasInvestigationRestrictionAsync(input.Id, token))
                throw new StocktakeWorkflowException("Chốt đối soát rồi xác nhận chứng từ trên đợt gốc.");

            var requests = await repo.Query<StocktakeRecountRequest>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            if (requests.Any(r => r.Status is "REQUESTED" or "IN_PROGRESS"))
                throw new StocktakeWorkflowException("Cần xử lý yêu cầu đếm lại đang mở trước khi xác nhận.");
            var rounds = await repo.Query<StocktakeRound>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            var observations = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == input.Id).ToListAsync(token);
            var observation = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests)
                .SingleOrDefault(o => o.ObservationId == input.ObservationId);
            if (observation is null || !NeedsSourceReview(observation))
                throw new StocktakeWorkflowException("Chọn chênh lệch đang cần xác minh nguồn gốc.");

            var receipts = await CompletedReceiptsAsync(observation, warehouse, token);
            var receipt = receipts.SingleOrDefault(r => r.Key == input.Source);
            if (receipt is null)
                throw new StocktakeWorkflowException("Chứng từ phải đã hoàn tất, đúng serial, sản phẩm và kho nhận.");

            observation.ReviewStatus = "RESOLVED";
            repo.Add(new AuditLog
            {
                ActorUserId = actorId, DatabasePrincipal = "EWMS", TableName = SourceAudit,
                EntityKey = observation.ObservationId.ToString(), ActionType = "UPDATE", OccurredAt = DateTime.UtcNow,
                Reason = reason, AfterData = System.Text.Json.JsonSerializer.Serialize(new { receipt.Kind, receipt.Id, receipt.Code, receipt.Label })
            });
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    private static bool NeedsSourceReview(StocktakeObservation observation) =>
        observation.ExpectedQuantity == 0 && observation.ActualQuantity == 1
        && observation.ProductId.HasValue && observation.ReviewStatus != "RESOLVED";

    private async Task<List<StocktakeSourceReceipt>> CompletedReceiptsAsync(StocktakeObservation observation, int warehouse, CancellationToken ct)
    {
        var serial = await repo.Query<SerialItem>().AsNoTracking().SingleOrDefaultAsync(s =>
            s.SerialNumber == observation.ScannedSerial && s.ProductId == observation.ProductId, ct);
        if (!StocktakeReceivingEvidence.IsInWarehouse(serial, warehouse)) return [];
        var receipts = await StocktakeReceivingEvidence.FindAsync(repo, observation.ScannedSerial, warehouse, "DONE", ct);
        var postings = await repo.Query<SerialMovement>().AsNoTracking()
            .Where(m => m.SerialItemId == serial!.SerialItemId && m.ToWarehouseId == warehouse
                && m.ToState == "IN_WAREHOUSE" && m.OccurredAt >= observation.CapturedAt)
            .Select(m => new { m.EventType, m.InboundId, m.ReturnReceiptId, m.TransferReceiptId }).ToListAsync(ct);
        return receipts.Where(r => r.ProductId == observation.ProductId && r.CompletedAt >= observation.CapturedAt
            && postings.Any(m => r.Kind switch
            {
                "INBOUND" => m.EventType == "PURCHASE_RECEIPT" && m.InboundId == r.Id,
                "RETURN" => m.EventType == "CUSTOMER_RETURN" && m.ReturnReceiptId == r.Id,
                "TRANSFER" => m.EventType == "TRANSFER_RECEIPT" && m.TransferReceiptId == r.Id,
                _ => false
            })).ToList();
    }

    public async Task ConfirmNoSurplusAsync(int actorId, StocktakeNoSurplusInput input, CancellationToken ct)
    {
        var reason = Reason(input.Reason);
        var warehouse = await WarehouseAsync(actorId, input.Id, ct);
        await repo.TransactionAsync(actorId, warehouse, async token =>
        {
            await RequireManagerAsync(actorId, token);
            var plan = await PlanAsync(input.Id, input.Version, token);
            RequireReview(plan);
            await RequireNoOpenRoundAsync(input.Id, token);
            await RequireNoPendingAdjustmentAsync(input.Id, token);
            var investigations = (await repo.InvestigationsAsync(input.Id, token)).Where(i => i.StocktakeId == input.Id).ToList();
            var proof = investigations.Where(i => i.ObservationId == input.ObservationId)
                .OrderByDescending(i => i.RequestedAt).ThenByDescending(i => i.InvestigationStocktakeId).FirstOrDefault();
            if (proof is null || proof.StocktakeInvestigationId != input.InvestigationId
                || proof.Outcome != "NEEDS_REVIEW" || investigations.Any(i => i.ClosedAt == null))
                throw new StocktakeWorkflowException("Chọn lần đối soát đã chốt gần nhất của serial này.");

            var rounds = await repo.Query<StocktakeRound>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            var requests = await repo.Query<StocktakeRecountRequest>().Where(r => r.StocktakeId == input.Id).ToListAsync(token);
            if (requests.Any(r => r.Status is "REQUESTED" or "IN_PROGRESS"))
                throw new StocktakeWorkflowException("Cần xử lý yêu cầu đếm lại đang mở trước khi xác nhận.");
            var observations = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == input.Id).ToListAsync(token);
            var observation = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests)
                .SingleOrDefault(o => o.ObservationId == input.ObservationId);
            if (observation is null || observation.ExpectedQuantity != 0 || observation.ActualQuantity != 1
                || observation.SerialItemId.HasValue || observation.ReviewStatus == "RESOLVED" || proof.ClosedAt < observation.ScannedAt)
                throw new StocktakeWorkflowException("Kết quả đối soát không còn khớp chênh lệch hiện tại.");
            if (await repo.Query<StocktakeObservation>().AnyAsync(o => o.StocktakeId == proof.InvestigationStocktakeId
                && o.ScannedSerial == observation.ScannedSerial && o.ActualQuantity == 1, token))
                throw new StocktakeWorkflowException("Đợt đối soát đã ghi nhận serial này. Cần xác minh tiếp trước khi đóng chênh lệch.");
            if ((await CompletedReceiptsAsync(observation, warehouse, token)).Count > 0)
                throw new StocktakeWorkflowException("Đã có chứng từ tiếp nhận phù hợp. Xác nhận xử lý theo chứng từ nguồn.");
            if (await repo.Query<InventoryAdjustmentLine>().AnyAsync(l => l.StocktakeId == input.Id && l.IsActive
                && l.ScannedSerial == observation.ScannedSerial && l.Adjustment.Status == "APPLIED", token))
                throw new StocktakeWorkflowException("Serial đã được áp dụng điều chỉnh tồn kho.");

            observation.ReviewStatus = "RESOLVED";
            repo.Add(new AuditLog
            {
                ActorUserId = actorId, DatabasePrincipal = "EWMS", TableName = NoSurplusAudit,
                EntityKey = observation.ObservationId.ToString(), ActionType = "UPDATE", OccurredAt = DateTime.UtcNow,
                Reason = reason, AfterData = System.Text.Json.JsonSerializer.Serialize(new { proof.InvestigationStocktakeId })
            });
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

}
