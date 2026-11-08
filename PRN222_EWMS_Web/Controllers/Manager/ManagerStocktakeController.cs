using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Manager;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Controllers.Manager;

[Authorize(Roles = "MANAGER")]
public class ManagerStocktakeController(IManagerStocktakeService stocktakes, ILogger<ManagerStocktakeController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ManagerStocktakeFilter filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(new ManagerStocktakeListViewModel { Filter = filter });
        try { return View(await stocktakes.GetListAsync(filter, ct)); }
        catch (ManagerStocktakeException ex)
        {
            ModelState.AddModelError(ex.Field ?? "", ex.Message);
            return View(new ManagerStocktakeListViewModel { Filter = filter });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, [FromQuery] ManagerStocktakeDetailFilter filter, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        if (!ModelState.IsValid) return BadRequest("Bộ lọc kết quả kiểm kê không hợp lệ.");
        try
        {
            var model = await stocktakes.GetDetailsAsync(id, filter, ct);
            return model is null ? NotFound() : View(model);
        }
        catch (ManagerStocktakeException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? warehouseId, CancellationToken ct) =>
        View("Form", await stocktakes.CreateFormAsync(warehouseId, ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManagerStocktakeFormViewModel form, CancellationToken ct)
    {
        form.Id = null;
        form.Code = null;
        form.RowVersion = null;
        return await SaveFormAsync(form, ct);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        try
        {
            var model = await stocktakes.EditFormAsync(id, ct);
            return model is null ? NotFound() : View("Form", model);
        }
        catch (ManagerStocktakeException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, ManagerStocktakeFormViewModel form, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        form.Id = id;
        return await SaveFormAsync(form, ct);
    }

    [HttpGet]
    public async Task<IActionResult> PlanningData(int warehouseId, CancellationToken ct)
    {
        var form = await stocktakes.CreateFormAsync(warehouseId, ct);
        var warehouse = form.Warehouses.SingleOrDefault(w => w.Id == warehouseId);
        if (warehouse is null) return NotFound(new { message = "Kho không tồn tại, ngừng hoạt động hoặc ngoài thời hạn thuê." });
        return Json(new { warehouse, locations = form.Locations, staff = form.Staff, awaitingCount = form.AwaitingCount, warnings = form.Warnings });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var model = await stocktakes.GetDetailsAsync(id, new ManagerStocktakeDetailFilter(), ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(ManagerStocktakeDeleteInput input, CancellationToken ct)
    {
        if (input.Id <= 0) return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thiếu phiên bản dữ liệu. Vui lòng xem lại đợt kiểm kê trước khi xóa.";
            return RedirectToAction(nameof(Delete), new { id = input.Id });
        }
        if (!TryActor(out var actorId)) return Forbid();
        try
        {
            await stocktakes.DeleteAsync(input, actorId, ct);
            TempData["Success"] = "Đã xóa đợt kiểm kê chưa bắt đầu.";
            return RedirectToAction(nameof(Index));
        }
        catch (ManagerStocktakeException ex)
        {
            if (ex.StatusCode == 404) return NotFound();
            if (ex.StatusCode == 403) return Forbid();
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Cannot delete stocktake {StocktakeId}", input.Id);
            TempData["Error"] = "Chưa xóa được đợt kiểm kê. Dữ liệu không được xóa một phần; vui lòng tải lại trang.";
        }
        return RedirectToAction(nameof(Delete), new { id = input.Id });
    }

    private async Task<IActionResult> SaveFormAsync(ManagerStocktakeFormViewModel form, CancellationToken ct)
    {
        if (!TryActor(out var actorId)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var id = await stocktakes.SaveAsync(form, actorId, ct);
                TempData["Success"] = form.SaveMode == "PLANNED" ? "Đã lưu kế hoạch và phân công kiểm kê. Đợt chưa bắt đầu." : "Đã lưu nháp đợt kiểm kê.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ManagerStocktakeException ex)
            {
                if (ex.StatusCode == 404) return NotFound();
                if (ex.StatusCode == 403) return Forbid();
                if (ex.StatusCode == 409 && form.Id.HasValue)
                {
                    TempData["Error"] = ex.Message;
                    return RedirectToAction(nameof(Details), new { id = form.Id.Value });
                }
                ModelState.AddModelError(ex.Field ?? "", ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cannot save stocktake {StocktakeId}", form.Id);
                ModelState.AddModelError("", "Chưa lưu được kế hoạch. Vui lòng tải lại trang và kiểm tra thông tin phân công.");
            }
        }
        await stocktakes.PopulateFormAsync(form, ct);
        if (form.Id.HasValue) ModelState.Remove(nameof(form.WarehouseId));
        return View("Form", form);
    }

    private bool TryActor(out int actorId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId > 0;
}
