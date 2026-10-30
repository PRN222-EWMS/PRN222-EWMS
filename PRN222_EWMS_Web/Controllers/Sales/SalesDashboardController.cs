using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Sales;

namespace PRN222_EWMS_Web.Controllers.Sales;

[Authorize(Roles = "SALES_STAFF")]
public class SalesDashboardController(ISalesService sales) : Controller
{
    public async Task<IActionResult> Index() => View(await sales.GetDashboardAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
}
