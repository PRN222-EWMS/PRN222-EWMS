using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Controllers.Warehouse;

[Authorize(Roles = "MANAGER,WAREHOUSE_STAFF")]
[Route("Transfer")]
[Route("TransferOrder")]
public class TransferController(ITransferOrderService transfers) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() => User.IsInRole("WAREHOUSE_STAFF")
        ? RedirectToAction("Index", "WarehouseTransfer")
        : RedirectToAction("Index", "ManagerTransfer");

    [HttpGet("Details/{id:long}")]
    public IActionResult Details(long id) => RedirectToAction(nameof(Edit), new { id });

    [HttpGet("Create")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Create() => View(await transfers.GetCreateFormAsync());

    [HttpPost("Create")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTransferOrderViewModel model)
    {
        NormalizeBindingErrors(ModelState);
        if (ModelState.IsValid)
        {
            var result = await transfers.CreateTransferOrderAsync(model, CurrentUserId);
            if (result.Success)
            {
                TempData["Success"] = model.IsRelease
                    ? "Đã phát hành lệnh chuyển kho và giữ chỗ tồn nguồn."
                    : "Đã lưu bản nháp lệnh chuyển kho.";
                return RedirectToAction(nameof(Edit), new { id = result.TransferOrderId });
            }

            ApplyServiceErrors(ModelState, model.Assessment, result.Error);
        }

        await transfers.PopulateFormAsync(model);
        return View(model);
    }

    [HttpGet("Edit/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await transfers.GetEditFormAsync(id, CurrentUserId);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost("Edit/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, EditTransferOrderViewModel model)
    {
        if (id != model.TransferOrderId) return BadRequest();
        NormalizeBindingErrors(ModelState);

        if (ModelState.IsValid)
        {
            var result = await transfers.UpdateTransferOrderAsync(model, CurrentUserId);
            if (result.Success)
            {
                TempData["Success"] = "Đã cập nhật lệnh chuyển kho thành công.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            ApplyServiceErrors(ModelState, model.Assessment, result.Error);
        }

        var current = await transfers.GetEditFormAsync(id, CurrentUserId);
        if (current == null) return NotFound();
        if (!current.CanEdit)
        {
            ModelState.Clear();
            return View(current);
        }

        model.TransferCode = current.TransferCode;
        model.Status = current.Status;
        model.CanEdit = current.CanEdit;
        model.CanCancel = current.CanCancel;
        model.LockReason = current.LockReason;
        await transfers.PopulateFormAsync(model, id);
        return View(model);
    }

    [HttpGet("GetAvailableProducts")]
    public async Task<IActionResult> GetAvailableProducts(int warehouseId, string itemCondition = "GOOD", long? transferOrderId = null)
    {
        if (warehouseId <= 0 || itemCondition is not ("GOOD" or "DAMAGED")) return BadRequest();
        return Json(await transfers.GetAvailableProductsAsync(warehouseId, itemCondition, transferOrderId));
    }

    [HttpPost("Preview")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(CreateTransferOrderViewModel model, long? transferOrderId = null)
    {
        model.IsRelease = false;
        NormalizeBindingErrors(ModelState);

        // Ignore incomplete line rows during live background preview
        if (model.Lines != null)
        {
            model.Lines = model.Lines.Where(l => l.ProductId > 0 && l.Quantity > 0).ToList();
        }

        if (model.FromWarehouseId <= 0 || model.ToWarehouseId <= 0 || model.Lines == null || model.Lines.Count == 0)
        {
            return Json(new TransferAssessment());
        }

        if (transferOrderId.HasValue)
        {
            var current = await transfers.GetEditFormAsync(transferOrderId.Value, CurrentUserId);
            if (current == null || !current.CanEdit)
                return Conflict(new { message = current?.LockReason ?? "Lệnh không tồn tại." });
        }

        return Json(await transfers.AssessAsync(model, transferOrderId, forceRelease: false));
    }

    [HttpPost("Cancel/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id)
    {
        var result = await transfers.CancelTransferOrderAsync(id, CurrentUserId);

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["Success"] = "Đã hủy lệnh chuyển kho thành công.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private static void NormalizeBindingErrors(ModelStateDictionary modelState)
    {
        foreach (var (key, entry) in modelState.ToList())
        {
            if (entry is null || entry.Errors.Count == 0) continue;

            var hasRawBindingError = entry.Errors.Any(e =>
                e.ErrorMessage.Contains("is invalid", StringComparison.OrdinalIgnoreCase) ||
                e.ErrorMessage.Contains("is required", StringComparison.OrdinalIgnoreCase));

            if (!hasRawBindingError) continue;

            modelState.Remove(key);
            if (key == nameof(CreateTransferOrderViewModel.FromWarehouseId))
                modelState.AddModelError(key, "Vui lòng chọn kho nguồn.");
            else if (key == nameof(CreateTransferOrderViewModel.ToWarehouseId))
                modelState.AddModelError(key, "Vui lòng chọn kho đích.");
            else if (key.EndsWith(".ProductId", StringComparison.Ordinal))
                modelState.AddModelError(key, "Vui lòng chọn sản phẩm.");
            else if (key.EndsWith(".Quantity", StringComparison.Ordinal))
                modelState.AddModelError(key, "Số lượng phải là số nguyên từ 1 đến 1.000.000.");
            else
                modelState.AddModelError(key, "Giá trị nhập không hợp lệ.");
        }
    }

    private static void ApplyServiceErrors(ModelStateDictionary modelState, TransferAssessment? assessment, string? fallbackError)
    {
        if (assessment != null && assessment.FieldErrors.Count > 0)
        {
            foreach (var (key, message) in assessment.FieldErrors)
            {
                modelState.AddModelError(key, message);
            }

            var nonFieldErrors = assessment.Errors
                .Where(e => !assessment.FieldErrors.Values.Contains(e))
                .ToList();
            foreach (var err in nonFieldErrors)
            {
                modelState.AddModelError("", err);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackError))
        {
            modelState.AddModelError("", fallbackError);
        }
    }
}
