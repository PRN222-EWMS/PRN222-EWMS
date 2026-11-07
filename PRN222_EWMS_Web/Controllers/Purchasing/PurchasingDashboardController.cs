using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Purchasing;

namespace PRN222_EWMS_Web.Controllers.Purchasing;

[Authorize(Roles = "PURCHASING_STAFF")]
public class PurchasingDashboardController(IPurchasingDashboardService dashboardService) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await dashboardService.GetDashboardAsync(CurrentUserId);
        return View(model);
    }
}

