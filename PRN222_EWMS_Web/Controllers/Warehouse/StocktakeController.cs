using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;

namespace PRN222_EWMS_Web.Controllers.Warehouse;

[Authorize(Roles = "WAREHOUSE_STAFF")]
public class StocktakeController(IStocktakeExecutionService service, IStocktakeHandlingService handling, ILogger<StocktakeController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        try { return View(await service.GetListAsync(ActorId, search, page, default)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpGet]
    public Task<IActionResult> Details(long id, long? recountRequestId) => PageAsync("Details", id, recountRequestId);

    [HttpGet]
    public Task<IActionResult> Count(long id, long? recountRequestId) => PageAsync("Count", id, recountRequestId);

    [HttpGet]
    public async Task<IActionResult> Handle(long id, long handlingId, long? recountRequestId)
    {
        try { return View(await handling.GetAsync(ActorId, id, handlingId, recountRequestId, default)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Move(StocktakeMoveInput input) =>
        MutateAsync(input, () => handling.MoveAsync(ActorId, input, default), "Đã xác nhận vị trí và tình trạng hàng. Số lượng tồn không thay đổi.", "Count");

    [HttpGet]
    public async Task<IActionResult> LookupSerial(long id, long? recountRequestId, string serial)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Json(await service.LookupSerialAsync(ActorId, id, recountRequestId, serial, default)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    private async Task<IActionResult> PageAsync(string view, long id, long? requestId)
    {
        if (id <= 0) return NotFound();
        try
        {
            var model = await service.GetDetailsAsync(ActorId, id, requestId, default);
            if (model is null) return NotFound();
            return View(view, model);
        }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Start(StocktakeActionInput input) =>
        MutateAsync(input, () => service.StartAsync(ActorId, input, default), "Đã bắt đầu nhiệm vụ kiểm kê.", "Count");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Scan(StocktakeCountInput input) =>
        MutateAsync(input, () => service.ScanAsync(ActorId, input, default), $"Đã ghi nhận serial {input.Serial?.Trim()}. Bạn có thể tiếp tục kiểm kê.", "Count");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(StocktakeActionInput input) =>
        MutateAsync(input, () => service.SubmitAsync(ActorId, input, default), "Đã gửi kết quả kiểm kê.", "Details");

    private async Task<IActionResult> MutateAsync(StocktakeActionInput input, Func<Task> action, string success, string destination)
    {
        if (input.Id <= 0) return NotFound();
        if (!ModelState.IsValid) return await PageAsync(destination, input.Id, input.RecountRequestId);
        try { await action(); TempData["Success"] = success; }
        catch (StocktakeWorkflowException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Dữ liệu vừa thay đổi. Hãy kiểm tra trang mới rồi thao tác lại."; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            logger.LogError(ex, "Stocktake write failed for {Id}", input.Id);
            TempData["Error"] = "Không thể lưu do dữ liệu thay đổi hoặc ràng buộc database. Hãy tải lại và thử lại.";
        }
        return RedirectToAction(destination, new { id = input.Id, recountRequestId = input.RecountRequestId });
    }
}
