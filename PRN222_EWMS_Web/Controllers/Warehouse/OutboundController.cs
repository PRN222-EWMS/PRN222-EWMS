using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Outbound;

namespace PRN222_EWMS_Web.Controllers.Warehouse;

[Authorize(Roles = "WAREHOUSE_STAFF,MANAGER")]
public class OutboundController : Controller
{
    private readonly IOutboundService _outboundService;
    private readonly ILogger<OutboundController> _logger;

    public OutboundController(IOutboundService outboundService, ILogger<OutboundController> logger)
    {
        _outboundService = outboundService;
        _logger = logger;
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string CurrentUserRole => User.IsInRole("MANAGER") ? "MANAGER" : "WAREHOUSE_STAFF";

    // =========================================================================
    // 1. DANH SÁCH PHIẾU XUẤT KHO
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        int? warehouseId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page = 1)
    {
        var model = await _outboundService.GetListAsync(
            CurrentUserId,
            CurrentUserRole,
            search,
            status,
            warehouseId,
            fromDate,
            toDate,
            page,
            pageSize: 10);

        return View(model);
    }

    // =========================================================================
    // 2. CHỨC NĂNG 27: CHI TIẾT PHIẾU XUẤT KHO (READ-ONLY)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var model = await _outboundService.GetDetailsAsync(id, CurrentUserId, CurrentUserRole);

        if (model == null)
        {
            TempData["Error"] = "Không tìm thấy phiếu xuất kho hoặc bạn không có quyền truy cập phiếu này.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    // =========================================================================
    // 3. CHỨC NĂNG 70: TẠO PHIẾU XUẤT KHO & PHIÊN LẤY HÀNG (PICKING WORKBENCH)
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Create(long? soId = null, long? salesOrderId = null)
    {
        var targetSoId = soId ?? salesOrderId;
        var eligibleOrders = await _outboundService.GetEligibleSalesOrdersAsync(CurrentUserId);

        var model = new CreateOutboundViewModel
        {
            SelectedSalesOrderId = targetSoId,
            EligibleOrders = eligibleOrders
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> StartPicking(long salesOrderId, string? requestToken)
    {
        var result = await _outboundService.StartPickingSessionAsync(salesOrderId, CurrentUserId, requestToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Create), new { soId = salesOrderId });
        }

        TempData["Success"] = "Đã mở phiếu xuất kho.";
        return RedirectToAction(nameof(Picking), new { id = result.OutboundId });
    }

    [HttpGet]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Picking(long id)
    {
        var model = await _outboundService.GetPickingWorkbenchAsync(id, CurrentUserId);

        if (model == null)
        {
            TempData["Error"] = "Không tìm thấy phiên lấy hàng hoặc bạn không có quyền truy cập.";
            return RedirectToAction(nameof(Index));
        }

        if (model.Status is "DONE" or "CANCELLED")
            return RedirectToAction(nameof(Details), new { id });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> ScanSerial(long outboundId, string serialNumber, string? version)
    {
        var result = await _outboundService.ScanSerialAsync(outboundId, serialNumber, CurrentUserId, version ?? "");

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Success"] = $"Đã thêm serial '{serialNumber.Trim()}' thành công!";
        }

        return RedirectToAction(nameof(Picking), new { id = outboundId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> RemoveSerial(long outboundId, long serialItemId, string? version)
    {
        var result = await _outboundService.RemoveSerialAsync(outboundId, serialItemId, CurrentUserId, version ?? "");

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Success"] = "Đã bỏ chọn serial.";
        }

        return RedirectToAction(nameof(Picking), new { id = outboundId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> Complete(long outboundId, string? version)
    {
        var result = await _outboundService.CompleteAsync(outboundId, CurrentUserId, version ?? "");

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Picking), new { id = outboundId });
        }

        TempData["Success"] = "Đã hoàn tất phiếu xuất và ghi nhận xuất kho.";
        return RedirectToAction(nameof(Details), new { id = outboundId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "WAREHOUSE_STAFF")]
    public async Task<IActionResult> CancelPicking(long outboundId, string? version)
    {
        var result = await _outboundService.CancelPickingAsync(outboundId, CurrentUserId, version ?? "");

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Picking), new { id = outboundId });
        }

        TempData["Success"] = "Đã hủy phiên lấy hàng và giải phóng các thiết bị.";
        return RedirectToAction(nameof(Index));
    }
}

