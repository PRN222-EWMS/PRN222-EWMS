using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Purchasing;
using PRN222_EWMS_Web.ViewModels.Purchasing;

namespace PRN222_EWMS_Web.Controllers.Purchasing;

[Authorize(Roles = "MANAGER,PURCHASING_STAFF,WAREHOUSE_STAFF")]
public class PurchaseOrderController(
    IPurchaseOrderService poService, 
    IWarehouseCapacityEngine capacityEngine) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserRole => User.FindFirstValue(ClaimTypes.Role)!;

    // =========================================================================
    // 1. DANH SÁCH ĐƠN MUA HÀNG (MANAGER & PURCHASING STAFF)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Index(PoListViewModel filter)
    {
        if (CurrentUserRole == "WAREHOUSE_STAFF")
        {
            return RedirectToAction("PurchaseOrders", "Inbound");
        }

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
        {
            ModelState.AddModelError(nameof(filter.ToDate), "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        var model = await poService.GetOrdersAsync(filter, CurrentUserId, CurrentUserRole);
        return View(model);
    }

    // =========================================================================
    // 2. CHI TIẾT ĐƠN MUA HÀNG (MANAGER & PURCHASING STAFF)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (CurrentUserRole == "WAREHOUSE_STAFF")
        {
            return RedirectToAction("PurchaseOrderDetails", "Inbound", new { id });
        }

        var model = await poService.GetOrderDetailsAsync(id, CurrentUserId, CurrentUserRole);
        if (model == null) return NotFound();

        return View(model);
    }

    // =========================================================================
    // 3. TẠO MỚI ĐƠN MUA HÀNG (PURCHASING STAFF)
    // =========================================================================
    [HttpGet]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Create()
    {
        var model = await poService.GetCreateFormAsync(CurrentUserId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Create(PoFormViewModel model, string action)
    {
        bool confirmNow = (action == "confirm");

        if (!ModelState.IsValid)
        {
            // Nạp lại danh mục nếu form không hợp lệ
            var reloadModel = await poService.GetCreateFormAsync(CurrentUserId);
            model.Warehouses = reloadModel.Warehouses;
            model.Suppliers = reloadModel.Suppliers;
            if (model.SupplierId > 0)
            {
                model.AvailableProducts = await poService.GetProductsBySupplierAsync(model.SupplierId);
            }
            return View(model);
        }

        var (success, message, poId) = await poService.CreateOrderAsync(model, confirmNow, CurrentUserId);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            var reloadModel = await poService.GetCreateFormAsync(CurrentUserId);
            model.Warehouses = reloadModel.Warehouses;
            model.Suppliers = reloadModel.Suppliers;
            if (model.SupplierId > 0)
            {
                model.AvailableProducts = await poService.GetProductsBySupplierAsync(model.SupplierId);
            }
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Details), new { id = poId });
    }

    // =========================================================================
    // 4. CHỈNH SỬA ĐƠN MUA HÀNG (PURCHASING STAFF & MANAGER)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await poService.GetEditFormAsync(id, CurrentUserId, CurrentUserRole);
        if (model == null)
        {
            TempData["ErrorMessage"] = "Đơn mua hàng không tồn tại hoặc đã phát sinh phiếu nhận hàng (Inbound) nên đã bị khóa sửa.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] long id, PoFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var reload = await poService.GetEditFormAsync(id, CurrentUserId, CurrentUserRole);
            if (reload != null)
            {
                model.Warehouses = reload.Warehouses;
                model.Suppliers = reload.Suppliers;
                model.AvailableProducts = reload.AvailableProducts;
                model.Pocode = reload.Pocode;
                model.Status = reload.Status;
            }
            return View(model);
        }

        var (success, message) = await poService.UpdateOrderAsync(id, model, CurrentUserId, CurrentUserRole);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            var reload = await poService.GetEditFormAsync(id, CurrentUserId, CurrentUserRole);
            if (reload != null)
            {
                model.Warehouses = reload.Warehouses;
                model.Suppliers = reload.Suppliers;
                model.AvailableProducts = reload.AvailableProducts;
                model.Pocode = reload.Pocode;
                model.Status = reload.Status;
            }
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    // =========================================================================
    // 5. XÁC NHẬN ĐƠN MUA HÀNG (CONFIRM PO)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Confirm(long id)
    {
        var (success, message) = await poService.ConfirmOrderAsync(id, CurrentUserId, CurrentUserRole);

        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Details), new { id });
    }

    // =========================================================================
    // 6. ĐÓNG SỐ LƯỢNG CÒN LẠI (CLOSE REMAINDER)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseRemainder(CloseRemainderRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do đóng số lượng còn lại.";
            return RedirectToAction(nameof(Details), new { id = request.PurchaseOrderId });
        }

        var (success, message) = await poService.CloseRemainderAsync(request.PurchaseOrderId, request.Reason, CurrentUserId, CurrentUserRole);

        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        return RedirectToAction(nameof(Details), new { id = request.PurchaseOrderId });
    }

    // =========================================================================
    // 7. API AJAX LẤY SẢN PHẨM THEO NHÀ CUNG CẤP
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetSupplierProducts(int supplierId)
    {
        var products = await poService.GetProductsBySupplierAsync(supplierId);
        return Json(products);
    }

    // =========================================================================
    // 8. API AJAX KIỂM TRA SỨC CHỨA KHO (CAPACITY CHECK)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> CheckCapacity(int warehouseId, int productId, int quantity = 1, long? excludePoId = null)
    {
        if (warehouseId <= 0 || productId <= 0)
        {
            return BadRequest(new { message = "warehouseId và productId phải hợp lệ." });
        }

        var result = await capacityEngine.CalculateCapacityAsync(warehouseId, productId, quantity, excludePoId);
        return Json(result);
    }
}
