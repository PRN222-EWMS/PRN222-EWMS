using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.WarehouseStaff;

namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;

[Authorize(Roles = "WAREHOUSE_STAFF,MANAGER")]
public class InboundController(IWarehouseStaffService warehouseService) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // =========================================================================
    // 1. DANH SÁCH ĐƠN MUA HÀNG CẦN NHẬP (USER STORY 10)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Index(string? search, DateOnly? fromDate, DateOnly? toDate, int page = 1)
    {
        return await PurchaseOrders(search, fromDate, toDate, page);
    }

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
    public async Task<IActionResult> Create(long? purchaseOrderId)
    {
        if (!purchaseOrderId.HasValue)
        {
            return RedirectToAction(nameof(PurchaseOrders));
        }

        var po = await warehouseService.GetReceivableOrderDetailsAsync(purchaseOrderId.Value, CurrentUserId);
        if (po == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        ViewBag.PoCode = po.Pocode;
        ViewBag.PurchaseOrderId = po.PurchaseOrderId;
        ViewBag.SupplierName = po.SupplierName;
        return View(po);
    }
}
