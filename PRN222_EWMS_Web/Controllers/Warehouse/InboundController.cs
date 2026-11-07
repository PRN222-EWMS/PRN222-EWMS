using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.Inbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;

[Authorize(Roles = "WAREHOUSE_STAFF,MANAGER")]
public class InboundController(IWarehouseStaffService warehouseService, IInboundService inbounds, ILogger<InboundController> logger) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // =========================================================================
    // 1. DANH SÁCH ĐƠN MUA HÀNG CẦN NHẬP (USER STORY 10)
    // =========================================================================
    [HttpGet]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public Task<IActionResult> Index(string? search = null, string? status = null, DateOnly? fromDate = null, DateOnly? toDate = null, int page = 1) =>
        ListReceipts(search, status, fromDate, toDate, page);

    private async Task<IActionResult> ListReceipts(string? search, string? status, DateOnly? fromDate, DateOnly? toDate, int page) =>
        View("Index", await inbounds.ListAsync(CurrentUserId, search, status, fromDate, toDate, page));

    [HttpGet]
    public async Task<IActionResult> PurchaseOrders(string? search, DateOnly? fromDate, DateOnly? toDate, int page = 1)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            ModelState.AddModelError(nameof(toDate), "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        var model = await warehouseService.GetReceivableOrdersAsync(CurrentUserId, search, fromDate, toDate, page, 10);
        return View("PurchaseOrders", model);
    }

    // =========================================================================
    // 2. CHI TIẾT ĐƠN MUA ĐỂ ĐỐI CHIẾU TRƯỚC KHI NHẬP (USER STORY 11)
    // =========================================================================
    [HttpGet]
    [Route("Inbound/PurchaseOrderDetails/{id:long}")]
    [Route("Warehouse/PurchaseOrder/Details/{id:long}")]
    public async Task<IActionResult> PurchaseOrderDetails(long id)
    {
        var model = await warehouseService.GetReceivableOrderDetailsAsync(id, CurrentUserId);

        // BẢO VỆ CÔ LẬP THEO KHO (WAREHOUSE ISOLATION - TEST SCENARIO 3)
        // Nếu đơn hàng không tồn tại hoặc thuộc kho khác: Trả về 403 Forbidden
        if (model == null)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("AccessDenied");
        }

        return View(model);
    }

    // =========================================================================
    // 3. KHỞI TẠO PHIẾU NHẬP HÀNG (INBOUND RECEIPT HOOK)
    // =========================================================================
    [HttpGet]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Create(long? purchaseOrderId) => View(await inbounds.GetCreateAsync(CurrentUserId, purchaseOrderId));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Create(InboundCreateViewModel input)
    {
        if (!ModelState.IsValid) return View(await ReloadCreateAsync(input));
        try { return RedirectToAction(nameof(Details), new { id = await inbounds.CreateDraftAsync(CurrentUserId, input) }); }
        catch (Exception ex) when (IsBusinessError(ex))
        {
            ModelState.AddModelError("", ErrorMessage(ex));
            return View(await ReloadCreateAsync(input));
        }
    }

    private async Task<InboundCreateViewModel> ReloadCreateAsync(InboundCreateViewModel input)
    {
        input.Orders = (await inbounds.GetCreateAsync(CurrentUserId)).Orders;
        return input;
    }

    [HttpGet]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Details(long id)
    {
        var model = await inbounds.GetDetailAsync(CurrentUserId, id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> AddItem(long id, [Bind(Prefix = "NewSerial")] ReceiveSerialInput input)
    {
        if (!ModelState.IsValid)
        {
            var message = SerialValidationMessage();
            return IsAjaxRequest() ? BadRequest(new { ok = false, message }) : await DetailErrorAsync(id, message, input);
        }
        try
        {
            await inbounds.AddItemAsync(CurrentUserId, id, input);
            if (IsAjaxRequest()) return Json(new { ok = true, message = "Đã thêm Serial vào phiếu nháp." });
            TempData["Success"] = "Đã thêm Serial.";
        }
        catch (Exception ex) when (IsBusinessError(ex))
        {
            logger.LogWarning(ex, "Could not add serial to inbound {InboundId}", id);
            var message = ErrorMessage(ex);
            return IsAjaxRequest() ? BadRequest(new { ok = false, message }) : await DetailErrorAsync(id, message, input);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> UpdateItem(long id, long itemId, ReceiveSerialInput input)
    {
        if (!ModelState.IsValid)
            return IsAjaxRequest() ? BadRequest(new { ok = false, message = SerialValidationMessage() }) : await DetailErrorAsync(id, SerialValidationMessage(), input);
        try
        {
            await inbounds.UpdateItemAsync(CurrentUserId, id, itemId, input);
            if (IsAjaxRequest()) return Json(new { ok = true, message = "Đã sửa Serial." });
            TempData["Success"] = "Đã sửa Serial.";
        }
        catch (Exception ex) when (IsBusinessError(ex))
        {
            logger.LogWarning(ex, "Could not update serial {ItemId} in inbound {InboundId}", itemId, id);
            return IsAjaxRequest() ? BadRequest(new { ok = false, message = ErrorMessage(ex) }) : await DetailErrorAsync(id, ErrorMessage(ex), input);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> RemoveItem(long id, long itemId, string version)
    {
        try
        {
            await inbounds.RemoveItemAsync(CurrentUserId, id, itemId, version);
            if (IsAjaxRequest()) return Json(new { ok = true, message = "Đã hủy Serial nháp." });
            TempData["Success"] = "Đã hủy Serial nháp.";
        }
        catch (Exception ex) when (IsBusinessError(ex))
        {
            logger.LogWarning(ex, "Could not remove serial {ItemId} from inbound {InboundId}", itemId, id);
            if (IsAjaxRequest()) return BadRequest(new { ok = false, message = ErrorMessage(ex) });
            TempData["Error"] = ErrorMessage(ex);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Complete(long id, string version)
    {
        try { await inbounds.CompleteAsync(CurrentUserId, id, version); TempData["Success"] = "Đã hoàn tất phiếu nhập."; }
        catch (Exception ex) when (IsBusinessError(ex)) { logger.LogWarning(ex, "Could not complete inbound {InboundId}", id); TempData["Error"] = ErrorMessage(ex); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Cancel(long id, string version, string reason)
    {
        try { await inbounds.CancelAsync(CurrentUserId, id, version, reason); TempData["Success"] = "Đã hủy phiếu nhập."; }
        catch (Exception ex) when (IsBusinessError(ex)) { TempData["Error"] = ErrorMessage(ex); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> PendingPutaway() => View(await inbounds.GetPendingPutawayAsync(CurrentUserId));

    private async Task<IActionResult> DetailErrorAsync(long id, string message, ReceiveSerialInput input)
    {
        var model = await inbounds.GetDetailAsync(CurrentUserId, id);
        if (model is null) return NotFound();
        ModelState.AddModelError("", message);
        model.NewSerial = input;
        return View("Details", model);
    }

    private static bool IsBusinessError(Exception ex) => ex is ArgumentException or InvalidOperationException
        or DbUpdateConcurrencyException or DbUpdateException or SqlException;

    private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private string SerialValidationMessage()
    {
        var invalidKeys = ModelState.Where(entry => entry.Value?.Errors.Count > 0).Select(entry => entry.Key).ToArray();
        if (invalidKeys.Any(key => key.EndsWith("LineId", StringComparison.OrdinalIgnoreCase)))
            return "Hãy chọn sản phẩm thuộc dòng PO còn số lượng nhận.";
        if (invalidKeys.Any(key => key.EndsWith("Month", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Year", StringComparison.OrdinalIgnoreCase)))
            return "Hãy nhập tháng (1–12) và năm sản xuất hợp lệ.";
        if (invalidKeys.Any(key => key.EndsWith("Serial", StringComparison.OrdinalIgnoreCase)))
            return "Hãy nhập hoặc quét Serial dài 1–100 ký tự.";
        return "Thông tin Serial chưa hợp lệ. Kiểm tra sản phẩm, tháng/năm, tình trạng và ghi chú rồi thử lại.";
    }

    private static string ErrorMessage(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException => "Phiếu đã được thay đổi. Vui lòng tải lại trang.",
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } or SqlException { Number: 2601 or 2627 }
            => "Serial này đã có trong phiếu đang hiệu lực hoặc trong tồn kho. Hãy đối chiếu mã trên tem.",
        DbUpdateException { InnerException: SqlException { Number: 207 or 208 } } or SqlException { Number: 207 or 208 }
            => "Database chưa có đủ bảng/cột cho Inbound mới. Cần áp dụng script docs/sql/inbound-capture-metadata.sql.",
        DbUpdateException { InnerException: SqlException { Number: 547 } sql } => ConstraintMessage(sql),
        SqlException { Number: 547 } sql => ConstraintMessage(sql),
        DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 } } or SqlException { Number: 1205 or 1222 }
            => "Database đang xử lý một thao tác khác trên phiếu này. Hãy thử lại sau ít giây.",
        DbUpdateException { InnerException: SqlException { Number: >= 50000 and <= 59999 } sql } => $"Database từ chối hoàn tất phiếu (SQL {sql.Number}): {sql.Message}",
        SqlException { Number: >= 50000 and <= 59999 } sql => $"Database từ chối hoàn tất phiếu (SQL {sql.Number}): {sql.Message}",
        DbUpdateException { InnerException: SqlException sql } => $"Database từ chối lưu Serial (mã SQL {sql.Number}). Vui lòng báo quản trị viên kiểm tra log.",
        SqlException sql => $"Database từ chối lưu Serial (mã SQL {sql.Number}). Vui lòng báo quản trị viên kiểm tra log.",
        DbUpdateException => "Database từ chối lưu Serial do ràng buộc dữ liệu. Vui lòng báo quản trị viên kiểm tra log.",
        _ => ex.Message
    };

    private static string ConstraintMessage(SqlException sql)
    {
        var match = Regex.Match(sql.Message, "(?:constraint|ràng buộc)\\s+[\"'](?<name>[A-Za-z0-9_]+)[\"']", RegexOptions.IgnoreCase);
        return match.Success
            ? $"Không thể lưu phiếu: dữ liệu vi phạm ràng buộc {match.Groups["name"].Value} (SQL 547). Vui lòng báo quản trị viên kèm mã này."
            : "Không thể lưu phiếu do ràng buộc dữ liệu (SQL 547). Vui lòng báo quản trị viên kiểm tra log của thao tác hoàn tất.";
    }
}
