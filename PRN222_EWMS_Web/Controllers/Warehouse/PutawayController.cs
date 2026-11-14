using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;

[Authorize(Roles = "WAREHOUSE_STAFF")]
public class PutawayController(IPutawayService putaway, ILogger<PutawayController> logger) : Controller
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PutawayFilter filter)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Inbound/PendingPutaway.cshtml", new PutawayIndexViewModel { LoadError = "Bộ lọc không hợp lệ. Hãy mở lại trang Chờ xếp kho." });
        }
        try { return View("~/Views/Inbound/PendingPutaway.cshtml", await putaway.GetIndexAsync(UserId, filter)); }
        catch (Exception ex) when (Handled(ex))
        {
            var error = Describe(ex);
            return View("~/Views/Inbound/PendingPutaway.cshtml", new PutawayIndexViewModel { LoadError = error.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Pending([FromQuery] PutawayFilter filter)
    {
        if (!ModelState.IsValid) return BadRequest("Bộ lọc nguồn, phiếu hoặc tìm kiếm không hợp lệ.");
        try { return PartialView("_Queue", (await putaway.GetIndexAsync(UserId, filter)).Queue); }
        catch (Exception ex) when (Handled(ex)) { var error = Describe(ex); return StatusCode(error.Status, error.Message); }
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(string serial)
    {
        if (!ModelState.IsValid) return BadRequest("Vui lòng nhập hoặc quét Serial.");
        try { return PartialView("_SerialDetails", await putaway.LookupAsync(UserId, serial)); }
        catch (Exception ex) when (Handled(ex)) { var error = Describe(ex); return StatusCode(error.Status, error.Message); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ConfirmPutawayInput input)
    {
        if (!ModelState.IsValid) return BadRequest(new { ok = false, message = "Thông tin xác nhận không hợp lệ. Hãy tra cứu lại Serial." });
        try
        {
            var result = await putaway.ConfirmAsync(UserId, input);
            return Json(new { ok = true, message = $"Đã xếp {result.Serial} tại {result.Path}.", result });
        }
        catch (Exception ex) when (Handled(ex))
        {
            var error = Describe(ex);
            return StatusCode(error.Status, new { ok = false, message = error.Message });
        }
    }

    private static bool Handled(Exception ex) => ex is PutawayException or DbException or DbUpdateException or TimeoutException;

    private (int Status, string Message) Describe(Exception ex)
    {
        if (ex is PutawayException business) return (business.StatusCode, business.Message);
        logger.LogError(ex, "Putaway could not complete for user {UserId}.", UserId);
        var sql = ex as SqlException ?? (ex as DbUpdateException)?.InnerException as SqlException;
        if (ex is DbUpdateConcurrencyException || sql?.Number is 1205 or 1222 or 547 or 2601 or 2627)
            return (409, "Serial hoặc vị trí vừa thay đổi/đang được xử lý. Hãy tải lại gợi ý để kiểm tra trước khi xác nhận lại.");
        return (503, "Không xử lý được Putaway lúc này. Hãy tra cứu lại Serial để kiểm tra trạng thái trước khi thử tiếp.");
    }
}
