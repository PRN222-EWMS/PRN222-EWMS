using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.Sales;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF,MANAGER,WAREHOUSE_STAFF")]
public class SalesOrderController(ISalesService sales, ILogger<SalesOrderController> logger) : Controller
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
                ? "Đã gửi đơn bán hàng và giữ hàng."
                : "Đã lưu bản nháp.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
        catch (SalesOrderSourceException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            await PopulateFormDataAsync(model);
            return View(model);
        }
        catch (Exception ex)
        {
            AddSaveError(ex);
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
            TempData["Error"] = "Chỉ người tạo được sửa SO Draft. SO đã gửi sẽ khóa nội dung.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, SalesOrderFormViewModel model, string action)
    {
        if (id != model.Id) return BadRequest();
        var current = await sales.GetEditFormAsync(id, CurrentUserId);
        if (current?.CanEdit != true) return Forbid();
        model.Status = current.Status;
        model.Code = current.Code;
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
                ? "Đã gửi đơn bán hàng và giữ hàng."
                : "Đã lưu thay đổi.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Dữ liệu đơn hàng đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang.");
            await PopulateFormDataAsync(model);
            return View(model);
        }
        catch (SalesOrderSourceException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            await PopulateFormDataAsync(model);
            return View(model);
        }
        catch (Exception ex)
        {
            AddSaveError(ex);
            await PopulateFormDataAsync(model);
            return View(model);
        }
    }

    [Authorize(Roles = "SALES_STAFF,MANAGER")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, string? reason, string? returnUrl = null)
    {
        var role = User.IsInRole("MANAGER") ? "MANAGER" : "SALES_STAFF";
        var (success, message) = await sales.CancelOrderAsync(id, CurrentUserId, role, reason);
        if (success)
        {
            TempData["Success"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }
        else
        {
            TempData["Error"] = message;
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCreateCustomer([FromForm] CustomerFormViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Dữ liệu nhập không hợp lệ.";
                return Json(new { success = false, message = firstError });
            }

            var (success, customer, errorMessage) = await sales.QuickCreateCustomerAsync(model, CurrentUserId);
            if (!success || customer == null)
            {
                return Json(new { success = false, message = errorMessage ?? "Không thể tạo khách hàng." });
            }

            var displayText = customer.CustomerName + (string.IsNullOrEmpty(customer.Phone) ? "" : $" ({customer.Phone})");
            return Json(new
            {
                success = true,
                customerId = customer.CustomerId,
                customerName = customer.CustomerName,
                phone = customer.Phone,
                address = customer.Address,
                displayText = displayText
            });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return Json(new { success = false, message = msg });
        }
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> GetAvailableProducts(int warehouseId, DateOnly? requestedDate)
    {
        var products = await sales.GetAvailableProductsAsync(warehouseId, requestedDate);
        return Json(products);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> GetSourceOptions(int warehouseId, DateOnly? requestedDate) =>
        Json(await sales.GetSourceOptionsAsync(warehouseId, requestedDate));

    [Authorize(Roles = "SALES_STAFF")]
    [HttpGet]
    public async Task<IActionResult> ReplaceSources(long id)
    {
        var model = await sales.GetReplacementFormAsync(id, CurrentUserId);
        return model == null ? NotFound() : View(model);
    }

    [Authorize(Roles = "SALES_STAFF")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceSources(long id, SalesOrderFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var fresh = await sales.GetReplacementFormAsync(id, CurrentUserId);
        if (fresh == null) return NotFound();
        try
        {
            if (ModelState.IsValid)
            {
                await sales.AddReplacementAsync(id, model, CurrentUserId);
                TempData["Success"] = "Đã bổ sung nguồn hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (SalesOrderSourceException ex) { ModelState.AddModelError(ex.Field, ex.Message); }
        catch (Exception ex) { AddSaveError(ex); }
        // Keep posted allocations for review; never silently replace the user's selections.
        foreach (var line in fresh.Lines)
            line.Sources = model.Lines.FirstOrDefault(l => l.SolineId == line.SolineId)?.Sources ?? [];
        fresh.ConcurrencyToken = model.ConcurrencyToken;
        return View(fresh);
    }

    private void AddSaveError(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
            ModelState.AddModelError(string.Empty, "SO đã thay đổi. Vui lòng tải lại trang trước khi lưu.");
        else if (ex is InvalidOperationException or UnauthorizedAccessException)
            ModelState.AddModelError(string.Empty, ex.Message);
        else
        {
            logger.LogError(ex, "Cannot save sales order for user {UserId}", CurrentUserId);
            ModelState.AddModelError(string.Empty, "Không thể lưu SO lúc này. Nguồn có thể vừa thay đổi; vui lòng tải lại số liệu và thử lại.");
        }
    }

    private async Task PopulateFormDataAsync(SalesOrderFormViewModel model)
    {
        var fresh = await sales.GetCreateFormAsync(CurrentUserId);
        model.Customers = fresh.Customers;
        model.Warehouses = fresh.Warehouses;
        if (string.IsNullOrEmpty(model.Code))
        {
            model.Code = fresh.Code;
        }
        if (model.WarehouseId.HasValue && model.WarehouseId.Value > 0)
        {
            model.AvailableProducts = await sales.GetAvailableProductsAsync(model.WarehouseId.Value, model.RequestedDate);
        }
        else
        {
            model.AvailableProducts = [];
        }
    }
}
