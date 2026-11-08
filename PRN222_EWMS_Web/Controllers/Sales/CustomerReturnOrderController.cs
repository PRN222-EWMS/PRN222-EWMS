using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Sales;
using PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF")]
public class CustomerReturnOrderController(ICustomerReturnOrderService returnOrderService) : Controller
{
    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // =========================================================================
    // 1. TẠO MỚI ĐƠN TRẢ HÀNG
    // =========================================================================
    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Create));

    [HttpGet]
    public async Task<IActionResult> Create(long? salesOrderId, CancellationToken cancellationToken) =>
        View(await returnOrderService.GetCreateViewModelAsync(salesOrderId, cancellationToken));

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
            : RedirectToAction(nameof(Create));
    }

    // =========================================================================
    // 2. CHỈNH SỬA BẢN NHÁP ĐƠN TRẢ HÀNG
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        if (id <= 0) return NotFound();

        var model = await returnOrderService.GetEditViewModelAsync(id, CurrentUserId, cancellationToken);
        if (model is null)
        {
            TempData["Error"] = $"Đơn trả hàng #{id} không tồn tại hoặc không ở trạng thái Bản nháp (DRAFT). Chỉ đơn bản nháp mới được phép chỉnh sửa.";
            return RedirectToAction(nameof(Create));
        }

        return View(model);
    }

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
            : RedirectToAction(nameof(Create));
    }

    // =========================================================================
    // 3. AJAX: TẢI DANH SÁCH DÒNG ĐƯỢC PHÉP TRẢ THEO ĐƠN BÁN HÀNG
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetEligibleLines(long salesOrderId, CancellationToken cancellationToken) =>
        salesOrderId <= 0
            ? Json(Array.Empty<object>())
            : Json(await returnOrderService.GetEligibleLinesAsync(salesOrderId, cancellationToken));
}
