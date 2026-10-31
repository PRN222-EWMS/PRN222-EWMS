using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.WarehouseStaff;

namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;

[Authorize(Roles = "WAREHOUSE_STAFF,MANAGER")]
public class WarehouseDashboardController(IWarehouseStaffService warehouseService) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await warehouseService.GetDashboardAsync(CurrentUserId);
        return View(model);
    }
}
