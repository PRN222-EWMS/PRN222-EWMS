using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.Sales;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF,MANAGER")]
public class SalesOrderController(ISalesService sales) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(SalesOrderListViewModel filter)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
            ModelState.AddModelError(nameof(filter.To), "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
        return View(await sales.GetOrdersAsync(filter, CurrentUserId));
    }

    public async Task<IActionResult> Details(long id)
    {
        var model = await sales.GetOrderAsync(id, CurrentUserId);
        return model is null ? NotFound() : View(model);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = await sales.GetCreateFormAsync(CurrentUserId);
        return View(model);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SalesOrderFormViewModel model, string action)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormDataAsync(model);
                return View(model);
            }

            var orderId = await sales.CreateOrderAsync(model, action, CurrentUserId);
            var isSubmit = string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase);
            TempData["Success"] = isSubmit
                ? "Đơn bán hàng đã được tạo và gửi thành công (đã cấp phát giữ chỗ kho)!"
                : "Đơn bán hàng đã được lưu nháp thành công!";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateFormDataAsync(model);
            return View(model);
        }
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await sales.GetEditFormAsync(id, CurrentUserId);
        if (model is null) return NotFound();

        if (!model.CanEdit)
        {
            TempData["Error"] = "Đơn bán hàng không đủ điều kiện chỉnh sửa (đơn đã có phiếu xuất hoặc bạn không phải người tạo đơn).";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, SalesOrderFormViewModel model, string action)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormDataAsync(model);
                return View(model);
            }

            await sales.UpdateOrderAsync(id, model, action, CurrentUserId);
            var isSubmit = string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase);
            TempData["Success"] = isSubmit
                ? "Đơn bán hàng đã được gửi thành công!"
                : "Đơn bán hàng đã được cập nhật thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Dữ liệu đơn hàng đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang.");
            await PopulateFormDataAsync(model);
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateFormDataAsync(model);
            return View(model);
        }
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> GetAvailableProducts(int warehouseId)
    {
        var products = await sales.GetAvailableProductsAsync(warehouseId);
        return Json(products);
    }

    private async Task PopulateFormDataAsync(SalesOrderFormViewModel model)
    {
        var fresh = await sales.GetCreateFormAsync(CurrentUserId);
        model.Customers = fresh.Customers;
        model.Warehouses = fresh.Warehouses;
        if (model.WarehouseId.HasValue && model.WarehouseId.Value > 0)
        {
            model.AvailableProducts = await sales.GetAvailableProductsAsync(model.WarehouseId.Value);
        }
        else
        {
            model.AvailableProducts = [];
        }
    }
}
