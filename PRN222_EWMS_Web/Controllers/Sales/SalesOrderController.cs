using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Sales;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF")]
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
}
