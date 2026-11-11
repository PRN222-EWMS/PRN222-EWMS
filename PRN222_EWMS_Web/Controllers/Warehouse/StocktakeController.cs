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
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        try { return View(await service.GetListAsync(ActorId, search, page, ct)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpGet]
    public Task<IActionResult> Details(long id, long? recountRequestId, CancellationToken ct) => PageAsync("Details", id, recountRequestId, ct);

    [HttpGet]
    public Task<IActionResult> Count(long id, long? recountRequestId, CancellationToken ct) => PageAsync("Count", id, recountRequestId, ct);

    [HttpGet]
    public async Task<IActionResult> Handle(long id, long handlingId, long? recountRequestId, CancellationToken ct)
    {
        try { return View(await handling.GetAsync(ActorId, id, handlingId, recountRequestId, ct)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Move(StocktakeMoveInput input, CancellationToken ct) =>
        MutateAsync(input, () => handling.MoveAsync(ActorId, input, ct), "Đã xác nhận chuyển hàng và cập nhật vị trí/tình trạng. Số lượng tồn không thay đổi.", "Count", ct);

    [HttpGet]
    public async Task<IActionResult> LookupSerial(long id, long? recountRequestId, string serial, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Json(await service.LookupSerialAsync(ActorId, id, recountRequestId, serial, ct)); }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    private async Task<IActionResult> PageAsync(string view, long id, long? requestId, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        try
        {
            var model = await service.GetDetailsAsync(ActorId, id, requestId, ct);
            if (model is null) return NotFound();
            return View(view, model);
        }
        catch (StocktakeWorkflowException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Start(StocktakeActionInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.StartAsync(ActorId, input, ct), "Đã bắt đầu nhiệm vụ kiểm kê.", "Count", ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Scan(StocktakeCountInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.ScanAsync(ActorId, input, ct), $"Đã ghi nhận serial {input.Serial?.Trim()}. Bạn có thể tiếp tục kiểm kê.", "Count", ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(StocktakeActionInput input, CancellationToken ct) =>
        MutateAsync(input, () => service.SubmitAsync(ActorId, input, ct), "Đã gửi kết quả kiểm kê.", "Details", ct);

    private async Task<IActionResult> MutateAsync(StocktakeActionInput input, Func<Task> action, string success, string destination, CancellationToken ct)
    {
        if (input.Id <= 0) return NotFound();
        if (!ModelState.IsValid) return await PageAsync(destination, input.Id, input.RecountRequestId, ct);
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
