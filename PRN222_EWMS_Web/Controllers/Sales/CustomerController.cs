using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Sales;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF")]
public class CustomerController(ISalesService sales) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(CustomerListViewModel filter) => View(await sales.GetCustomersAsync(filter));

    public async Task<IActionResult> Details(int id)
    {
        var model = await sales.GetCustomerAsync(id, CurrentUserId);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new CustomerFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var id = await sales.CreateCustomerAsync(model, CurrentUserId);
        TempData["Success"] = "Tạo khách hàng thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await sales.GetCustomerFormAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, CustomerFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        if (!await sales.UpdateCustomerAsync(id, model, CurrentUserId)) return NotFound();
        TempData["Success"] = "Cập nhật khách hàng thành công. Thông tin đã lưu trên chứng từ được giữ nguyên.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
