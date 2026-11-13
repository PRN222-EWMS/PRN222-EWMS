using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Repositories.WarehouseStaff;
using PRN222_EWMS_Web.Services.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.WarehouseTransfer;

namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;

[Authorize(Roles = "WAREHOUSE_STAFF")]
public class WarehouseTransferController(
    WarehouseTransferRepository repository, TransferDispatchWorkService dispatchWork,
    TransferReceiptWorkService receiptWork) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string work = "all", string direction = "all")
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId))
            return Forbid();

        search = search?.Trim();
        work = work is "available" or "mine" ? work : "all";
        direction = direction is "dispatch" or "receive" ? direction : "all";

        IEnumerable<TransferListRow> rows = await repository.ListAsync(userId);
        if (!string.IsNullOrEmpty(search))
            rows = rows.Where(row => row.Code.Contains(search, StringComparison.OrdinalIgnoreCase));

        rows = work switch
        {
            "available" => rows.Where(row => row.CanClaim),
            "mine" => rows.Where(row => row.CanContinue),
            _ => rows
        };
        rows = direction switch
        {
            "dispatch" => rows.Where(row => row.IsSource),
            "receive" => rows.Where(row => !row.IsSource),
            _ => rows
        };

        ViewBag.Search = search;
        ViewBag.Work = work;
        ViewBag.Direction = direction;
        return View(rows.ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, bool fragment = false)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId))
            return Forbid();

        var detail = await repository.GetDetailAsync(userId, id);
        if (detail is null)
            return NotFound();

        if (fragment)
        {
            if (!detail.Documents.Any(document => document.CanScan || document.CanScanReceipt))
                return Conflict(new { redirectUrl = Url.Action(nameof(Details), new { id }) });
            ViewData["PickingFragment"] = true;
            return PartialView("Details", detail);
        }
        return View(detail);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDispatchSerial(long id, long documentId, long itemId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();
        var result = await dispatchWork.RemoveSerialAsync(id, documentId, itemId, userId);
        return WorkResult(id, result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveReceiptSerial(long id, long documentId, long itemId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();
        var result = await receiptWork.RemoveSerialAsync(id, documentId, itemId, userId);
        return WorkResult(id, result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteReceipt(long id, long receiptId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var result = await receiptWork.CompleteAsync(id, receiptId, userId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanReceipt(long id, long receiptId, string? serialNumber,
        string? receivedCondition, string? conditionNote)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var result = await receiptWork.ScanAsync(id, receiptId, serialNumber, receivedCondition, conditionNote, userId);
        return WorkResult(id, result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimReceipt(long id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var result = await receiptWork.ClaimAsync(id, userId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), null, new { id }, result.Success ? "my-transfer-work" : null);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteDispatch(long id, long dispatchId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var result = await dispatchWork.CompleteAsync(id, dispatchId, userId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanDispatch(long id, long dispatchId, string? serialNumber)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var result = await dispatchWork.ScanAsync(id, dispatchId, serialNumber, userId);
        return WorkResult(id, result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanDispatchBatch(long id, long dispatchId, string[]? serialNumbers)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();
        var result = await dispatchWork.ScanBatchAsync(id, dispatchId, serialNumbers ?? [], userId);
        return WorkResult(id, result);
    }

    private IActionResult WorkResult(long id, (bool Success, string Message) result)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), null, new { id }, "my-transfer-work");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimDispatch(long id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var result = await dispatchWork.ClaimAsync(id, userId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), null, new { id }, result.Success ? "my-transfer-work" : null);
    }
}
