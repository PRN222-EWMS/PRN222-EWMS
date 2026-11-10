using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Controllers.Warehouse;

[Authorize(Roles = "MANAGER,WAREHOUSE_STAFF")]
[Route("Transfer")]
[Route("TransferOrder")]
public class TransferController : Controller
{
    private readonly ITransferOrderService _transferService;
    private readonly ILogger<TransferController> _logger;

    public TransferController(ITransferOrderService transferService, ILogger<TransferController> logger)
    {
        _transferService = transferService;
        _logger = logger;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserRole => User.IsInRole("MANAGER") ? "MANAGER" : "WAREHOUSE_STAFF";

    // =========================================================================
    // 1. DANH SÁCH LỆNH CHUYỂN KHO (MÀN HÌNH NÀY DO THÀNH VIÊN KHÁC PHỤ TRÁCH)
    // =========================================================================
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        // Điều hướng sang màn hình theo dõi chuyển kho của Quản lý
        return RedirectToAction("Index", "ManagerTransfer");
    }

    // =========================================================================
    // 2. CHI TIẾT LỆNH CHUYỂN KHO (MÀN HÌNH NÀY DO THÀNH VIÊN KHÁC PHỤ TRÁCH)
    // =========================================================================
    [HttpGet("Details/{id:long}")]
    public IActionResult Details(long id)
    {
        // Màn hình Details thuộc phạm vi của đồng đội. Tạm thời điều hướng vào Edit.
        return RedirectToAction(nameof(Edit), new { id });
    }

    // =========================================================================
    // 3. CHỨC NĂNG 37: TẠO LỆNH CHUYỂN KHO (MANAGER ONLY)
    // =========================================================================
    [HttpGet("Create")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Create()
    {
        var model = await _transferService.GetCreateFormAsync();
        return View(model);
    }

    [HttpPost("Create")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTransferOrderViewModel model)
    {
        var result = await _transferService.CreateTransferOrderAsync(model, CurrentUserId);

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            var form = await _transferService.GetCreateFormAsync();
            form.FromWarehouseId = model.FromWarehouseId;
            form.ToWarehouseId = model.ToWarehouseId;
            form.ItemCondition = model.ItemCondition;
            form.ExpectedDate = model.ExpectedDate;
            form.AssignedToUserId = model.AssignedToUserId;
            form.Note = model.Note;
            form.Lines = model.Lines;
            return View(form);
        }

        TempData["Success"] = model.IsRelease 
            ? "Đã tạo và phát hành (RELEASED) lệnh chuyển kho thành công! Nguồn tồn đã được giữ chỗ."
            : "Đã lưu bản nháp (DRAFT) lệnh chuyển kho thành công!";

        return RedirectToAction(nameof(Edit), new { id = result.TransferOrderId });
    }

    // =========================================================================
    // 4. CHỨC NĂNG 36: CHỈNH SỬA LỆNH CHUYỂN KHO (MANAGER ONLY)
    // =========================================================================
    [HttpGet("Edit/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await _transferService.GetEditFormAsync(id, CurrentUserId);

        if (model == null)
        {
            TempData["Error"] = "Lệnh chuyển kho không tồn tại hoặc đã ở trạng thái không thể chỉnh sửa.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost("Edit/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, EditTransferOrderViewModel model)
    {
        if (id != model.TransferOrderId)
        {
            return BadRequest();
        }

        var result = await _transferService.UpdateTransferOrderAsync(model, CurrentUserId);

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            var form = await _transferService.GetEditFormAsync(id, CurrentUserId);
            if (form == null) return RedirectToAction(nameof(Index));

            form.ExpectedDate = model.ExpectedDate;
            form.AssignedToUserId = model.AssignedToUserId;
            form.Note = model.Note;
            form.Lines = model.Lines;
            return View(form);
        }

        TempData["Success"] = "Cập nhật lệnh chuyển kho thành công!";
        return RedirectToAction(nameof(Edit), new { id = model.TransferOrderId });
    }

    // =========================================================================
    // 5. HỦY LỆNH CHUYỂN KHO (MANAGER ONLY)
    // =========================================================================
    [HttpPost("Cancel/{id:long}")]
    [Authorize(Roles = "MANAGER")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id)
    {
        var result = await _transferService.CancelTransferOrderAsync(id, CurrentUserId);

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["Success"] = "Đã hủy lệnh chuyển kho thành công!";
        return RedirectToAction(nameof(Edit), new { id });
    }
}
