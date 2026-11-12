using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.Ceo;
using PRN222_EWMS_Web.Services.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Ceo;
using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Controllers.Ceo;

[Authorize(Roles = "CEO")]
public class CeoStocktakeAdjustmentController(ICeoStocktakeAdjustmentService service, ILogger<CeoStocktakeAdjustmentController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, CancellationToken ct = default)
    {
        try { return View(await service.GetListAsync(ActorId, search, status, page, ct)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        try
        {
            var model = await service.GetDetailsAsync(ActorId, id, ct);
            return model is null ? NotFound() : View(model);
        }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(CeoStocktakeAdjustmentDecisionInput input, CancellationToken ct) => DecideAsync(input, true, ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(CeoStocktakeAdjustmentDecisionInput input, CancellationToken ct) => DecideAsync(input, false, ct);

    private async Task<IActionResult> DecideAsync(CeoStocktakeAdjustmentDecisionInput input, bool approve, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            try
            {
                var model = await service.GetDetailsAsync(ActorId, input.Id, ct);
                return model is null ? NotFound() : View("Details", model);
            }
            catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }
        try
        {
            await service.DecideAsync(ActorId, input, approve, ct);
            TempData["Success"] = approve ? "Đã duyệt và áp dụng điều chỉnh tồn kho." : "Đã từ chối yêu cầu. Manager có thể rà soát lại.";
        }
        catch (StocktakeWorkflowException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Dữ liệu vừa thay đổi. Hãy tải lại và kiểm tra trước khi xét duyệt."; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            logger.LogError(ex, "CEO stocktake decision failed for {Id}", input.Id);
            TempData["Error"] = "Không thể áp dụng do dữ liệu thay đổi hoặc ràng buộc database. Toàn bộ thao tác đã được hoàn tác.";
        }
        return RedirectToAction(nameof(Details), new { id = input.Id });
    }
}
