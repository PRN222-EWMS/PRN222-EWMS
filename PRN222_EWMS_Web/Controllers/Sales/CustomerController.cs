using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Customers;
using PRN222_EWMS_Web.ViewModels.Customer;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF")]
public class CustomerController(ICustomerService customers) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CustomerListFilter filter) =>
        View(await customers.GetCustomerListAsync(filter));

    [HttpGet]
    public async Task<IActionResult> Details(int id, [FromQuery] CustomerListFilter returnFilter)
    {
        var model = await customers.GetCustomerDetailsAsync(id, CurrentUserId, returnFilter);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public IActionResult Create([FromQuery] CustomerListFilter returnFilter) =>
        View(new CreateCustomerViewModel { ReturnFilter = returnFilter });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCustomerViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var (id, errors) = await customers.CreateAsync(model, CurrentUserId);
        AddErrors(errors);
        if (errors.Count != 0 || !id.HasValue) return View(model);
        TempData["Success"] = "Tạo khách hàng thành công.";
        return RedirectToAction(nameof(Details), new { id, search = model.ReturnFilter.Search, page = model.ReturnFilter.Page });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, [FromQuery] CustomerListFilter returnFilter)
    {
        var model = await customers.GetEditCustomerAsync(id, returnFilter);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, EditCustomerViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        var errors = await customers.UpdateAsync(id, model, CurrentUserId);
        AddErrors(errors);
        if (errors.Count != 0) return View(model);
        TempData["Success"] = "Cập nhật khách hàng thành công. Thông tin đã lưu trên chứng từ được giữ nguyên.";
        return RedirectToAction(nameof(Details), new { id, search = model.ReturnFilter.Search, page = model.ReturnFilter.Page });
    }

    private void AddErrors(Dictionary<string, string> errors)
    {
        foreach (var error in errors)
            ModelState.AddModelError(error.Key == "General" ? "" : error.Key, error.Value);
    }
}