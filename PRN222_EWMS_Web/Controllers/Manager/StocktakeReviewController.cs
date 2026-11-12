using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Controllers.Manager;

[Authorize(Roles = "MANAGER")]
public class StocktakeReviewController(IStocktakeReviewService service, ILogger<StocktakeReviewController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public Task<IActionResult> Details(long id, CancellationToken ct) => PageAsync("Details", id, ct);
    [HttpGet]
    public Task<IActionResult> Recount(long id, CancellationToken ct) => PageAsync("Recount", id, ct);
    [HttpGet]
    public Task<IActionResult> Adjustment(long id, CancellationToken ct) => PageAsync("Adjustment", id, ct);

    private async Task<IActionResult> PageAsync(string view, long id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        try
        {
            var model = await service.GetDetailsAsync(ActorId, id, ct);
            return model is null ? NotFound() : View(view, model);
        }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CreateRecount(StocktakeRecountInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.CreateRecountAsync(ActorId, input, ct), "Đã giao yêu cầu đếm lại.", "Recount", ct);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvestigation(StocktakeInvestigationInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await PageAsync("Details", input.Id, ct);
        try
        {
            var childId = await service.CreateInvestigationAsync(ActorId, input, ct);
            TempData["Success"] = "Đã tạo đợt đối soát toàn kho. Có thể sửa phân công tại chi tiết kế hoạch trước khi bắt đầu.";
            return RedirectToAction("Details", new { id = childId });
        }
        catch (StocktakeWorkflowException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Dữ liệu vừa thay đổi. Hãy tải lại."; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        { logger.LogError(ex, "Investigation creation failed for {Id}", input.Id); TempData["Error"] = "Không thể tạo yêu cầu đối soát. Thao tác đã hoàn tác."; }
        return RedirectToAction("Details", new { id = input.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> FinishInvestigation(StocktakeInvestigationConclusionInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.FinishInvestigationAsync(ActorId, input, ct), "Đã lưu kết luận đối soát; chưa thay đổi số lượng tồn kho.", "Details", ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitAdjustment(StocktakeAdjustmentInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.SubmitAdjustmentAsync(ActorId, input, ct), "Đã gửi yêu cầu điều chỉnh cho CEO. Tồn kho chưa thay đổi.", "Adjustment", ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(StocktakeActionInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.CompleteAsync(ActorId, input, ct), "Đã hoàn tất kiểm kê và giải phóng khóa phạm vi.", "Details", ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CancelRecount(StocktakeActionInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.CancelRecountAsync(ActorId, input, ct), "Đã hủy yêu cầu chưa bắt đầu; lịch sử vẫn được giữ.", "Details", ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ConfirmNoSurplus(StocktakeNoSurplusInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.ConfirmNoSurplusAsync(ActorId, input, ct), "Đã xác nhận không có hàng thừa.", "Details", ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ConfirmSource(StocktakeSourceResolutionInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.ConfirmSourceAsync(ActorId, input, ct), "Đã đóng chênh lệch theo chứng từ đã hoàn tất.", "Details", ct);

    private async Task<IActionResult> MutateAsync(StocktakeActionInput input, Func<Task> action, string success, string errorView, CancellationToken ct)
    {
        if (input.Id <= 0) return NotFound();
        if (!ModelState.IsValid) return await PageAsync(errorView, input.Id, ct);
        try { await action(); TempData["Success"] = success; }
        catch (StocktakeWorkflowException ex) { TempData["Error"] = ex.Message; return RedirectToAction(errorView, new { id = input.Id }); }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Dữ liệu vừa thay đổi. Hãy tải lại và kiểm tra trước khi gửi."; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            logger.LogError(ex, "Stocktake review write failed for {Id}", input.Id);
            TempData["Error"] = "Không thể lưu do dữ liệu thay đổi hoặc ràng buộc database. Hãy tải lại và thử lại.";
        }
        return RedirectToAction("Details", new { id = input.Id });
    }
}
