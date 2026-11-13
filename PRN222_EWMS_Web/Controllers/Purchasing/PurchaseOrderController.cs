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
            if (message.Contains("Mã đơn mua") || message.Contains("mã đơn mua"))
            {
                ModelState.AddModelError(nameof(model.Pocode), message);
            }
            else
            {
                ModelState.AddModelError(string.Empty, message);
            }
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
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await poService.GetEditFormAsync(id, CurrentUserId, CurrentUserRole);
        if (model == null)
        {
            TempData["ErrorMessage"] = "Chỉ người tạo được sửa PO Draft. PO đã Confirm khóa nội dung kể cả chưa có phiếu nhập.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Edit([FromRoute] long id, PoFormViewModel model, string action = "draft")
    {
        if (model.PurchaseOrderId != id) return BadRequest();
        var current = await poService.GetEditFormAsync(id, CurrentUserId, CurrentUserRole);
        if (current == null) return Forbid();
        model.Status = current.Status;
        model.Pocode = current.Pocode;
        bool confirmNow = string.Equals(action, "confirm", StringComparison.OrdinalIgnoreCase);

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

        if (confirmNow)
        {
            var (confirmSuccess, confirmMessage) = await poService.ConfirmOrderAsync(id, CurrentUserId, CurrentUserRole);
            if (!confirmSuccess)
            {
                TempData["ErrorMessage"] = "Đã lưu thay đổi nhưng chưa thể xác nhận đơn: " + confirmMessage;
                return RedirectToAction(nameof(Details), new { id });
            }
            TempData["SuccessMessage"] = "Đơn mua hàng đã được lưu và xác nhận thành công.";
            return RedirectToAction(nameof(Details), new { id });
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
    // 6.1. HỦY ĐƠN MUA HÀNG (CANCEL PO)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, string? returnUrl = null)
    {
        var (success, message) = await poService.CancelOrderAsync(id, CurrentUserId, CurrentUserRole);

        if (success)
            TempData["SuccessMessage"] = message;
        else
            TempData["ErrorMessage"] = message;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (success)
            return RedirectToAction(nameof(Details), new { id });
        else
            return RedirectToAction(nameof(Edit), new { id });
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
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "MANAGER,PURCHASING_STAFF")]
    public async Task<IActionResult> CheckCapacity([FromForm] PoFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid || form.WarehouseId <= 0 || form.Lines is null || form.Lines.Count is < 1 or > 100
            || form.Lines.Any(l => l.ProductId <= 0 || l.OrderedQuantity is < 1 or > 1000000)
            || form.Lines.Select(l => l.ProductId).Distinct().Count() != form.Lines.Count)
            return BadRequest(new { message = "Hãy chọn kho, ngày nhận và các sản phẩm khác nhau với số lượng hợp lệ." });
        if (form.PurchaseOrderId is long id && id > 0 && await poService.GetEditFormAsync(id, CurrentUserId, CurrentUserRole) == null)
            return Forbid();
        return Json(await capacityEngine.EvaluateAsync(new()
        {
            WarehouseId = form.WarehouseId, ExpectedArrivalDate = form.ExpectedArrivalDate,
            ExcludePoId = form.PurchaseOrderId > 0 ? form.PurchaseOrderId : null,
            Demands = form.Lines.Select(l => new CapacityDemand(l.ProductId, l.OrderedQuantity)).ToList(), CalculateLimits = true
        }, ct));
    }

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

    // =========================================================================
    // 9. API AJAX KIỂM TRA TRÙNG MÃ ĐƠN MUA (PO CODE DUPLICATE CHECK)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> CheckPoCode(string poCode)
    {
        if (string.IsNullOrWhiteSpace(poCode))
            return Json(new { isValid = true, isDuplicate = false });

        var trimmed = poCode.Trim();
        var exists = await poService.IsPoCodeDuplicateAsync(trimmed);
        return Json(new
        {
            isValid = !exists,
            isDuplicate = exists,
            message = exists ? $"Mã đơn mua '{trimmed}' đã tồn tại trong hệ thống. Vui lòng nhập mã khác." : null
        });
    }
}
