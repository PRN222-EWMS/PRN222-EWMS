using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public IActionResult Index() => RedirectToAction("Index", "ManagerTransfer");
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
        if (ModelState.IsValid)
        {
            var result = await transfers.CreateTransferOrderAsync(model, CurrentUserId);
            if (result.Success)
            {
                TempData["Success"] = model.IsRelease ? "Đã phát hành lệnh và giữ chỗ tồn nguồn." : "Đã lưu nháp. Tồn nguồn chưa được giữ chỗ.";
                TempData["TransferWarnings"] = string.Join("\n", model.Assessment.Warnings);
                return RedirectToAction(nameof(Edit), new { id = result.TransferOrderId });
            }
            ModelState.AddModelError("", result.Error!);
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
        if (ModelState.IsValid)
        {
            var result = await transfers.UpdateTransferOrderAsync(model, CurrentUserId);
            if (result.Success)
            {
                TempData["Success"] = "Đã cập nhật lệnh chuyển kho.";
                TempData["TransferWarnings"] = string.Join("\n", model.Assessment.Warnings);
                return RedirectToAction(nameof(Edit), new { id });
            }
            ModelState.AddModelError("", result.Error!);
        }
        var current = await transfers.GetEditFormAsync(id, CurrentUserId);
        if (current == null) return NotFound();
        if (!current.CanEdit) { ModelState.Clear(); return View(current); }
        model.TransferCode = current.TransferCode; model.Status = current.Status;
        model.CanEdit = current.CanEdit; model.CanCancel = current.CanCancel; model.LockReason = current.LockReason;
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
        if (!ModelState.IsValid) return Json(new TransferAssessment { Errors = ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Dữ liệu nhập không hợp lệ." : e.ErrorMessage).ToList() });
        bool forceRelease = false;
        if (transferOrderId.HasValue)
        {
            var current = await transfers.GetEditFormAsync(transferOrderId.Value, CurrentUserId);
            if (current == null || !current.CanEdit) return Conflict(new { message = current?.LockReason ?? "Lệnh không tồn tại." });
            forceRelease = current.Status == "RELEASED";
        }
        return Json(await transfers.AssessAsync(model, transferOrderId, forceRelease));
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

        TempData["Success"] = "Đã hủy lệnh chuyển kho thành công!";
        return RedirectToAction(nameof(Edit), new { id });
    }
}
