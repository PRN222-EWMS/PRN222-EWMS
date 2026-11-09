using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Sales;
using PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF,WAREHOUSE_STAFF")]
public class CustomerReturnOrderController(ICustomerReturnOrderService returnOrderService) : Controller
{
    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // =========================================================================
    // 1. DANH SÁCH ĐƠN TRẢ HÀNG (UC58)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ReturnOrderListFilterViewModel filter, CancellationToken cancellationToken)
    {
        if (CurrentUserId <= 0) return Challenge();

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
        {
            ModelState.AddModelError(nameof(filter.ToDate), "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        var model = await returnOrderService.GetReturnOrderListAsync(filter, CurrentUserId, cancellationToken);
        return View(model);
    }

    // =========================================================================
    // 2. TẠO MỚI ĐƠN TRẢ HÀNG (UC57)
    // =========================================================================
    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> Create(long? salesOrderId, CancellationToken cancellationToken) =>
        View(await returnOrderService.GetCreateViewModelAsync(salesOrderId, cancellationToken));

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReturnOrderFormModel model, CancellationToken cancellationToken)
    {
        if (CurrentUserId <= 0) return Challenge();

        if (!ModelState.IsValid)
        {
            await returnOrderService.PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var (success, message, newId, _) = await returnOrderService.CreateReturnOrderAsync(model, CurrentUserId, cancellationToken);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            await returnOrderService.PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = message;
        return model.Action == "SaveDraft"
            ? RedirectToAction(nameof(Edit), new { id = newId })
            : RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // 3. CHỈNH SỬA BẢN NHÁP ĐƠN TRẢ HÀNG (UC57)
    // =========================================================================
    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        if (id <= 0) return NotFound();

        var model = await returnOrderService.GetEditViewModelAsync(id, CurrentUserId, cancellationToken);
        if (model is null)
        {
            TempData["Error"] = $"Đơn trả hàng #{id} không tồn tại hoặc không ở trạng thái Bản nháp (DRAFT). Chỉ đơn bản nháp mới được phép chỉnh sửa.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, ReturnOrderFormModel model, CancellationToken cancellationToken)
    {
        if (CurrentUserId <= 0) return Challenge();

        model.ReturnOrderId = id;
        if (!ModelState.IsValid)
        {
            await returnOrderService.PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var (success, message, _) = await returnOrderService.UpdateDraftOrderAsync(id, model, CurrentUserId, cancellationToken);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            await returnOrderService.PopulateFormOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = message;
        return model.Action == "SaveDraft"
            ? RedirectToAction(nameof(Edit), new { id })
            : RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // 4. AJAX: TẢI DANH SÁCH DÒNG ĐƯỢC PHÉP TRẢ THEO ĐƠN BÁN HÀNG (UC57)
    // =========================================================================
    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> GetEligibleLines(long salesOrderId, CancellationToken cancellationToken) =>
        salesOrderId <= 0
            ? Json(Array.Empty<object>())
            : Json(await returnOrderService.GetEligibleLinesAsync(salesOrderId, cancellationToken));
}
