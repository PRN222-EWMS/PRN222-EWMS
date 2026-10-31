using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Manager;

namespace PRN222_EWMS_Web.Controllers.Manager;

[Authorize(Roles = "MANAGER")]
public class ManagerDashboardController(
    IManagerDashboardService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(int? warehouseId)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var model = await service.GetDashboardAsync(warehouseId);

        return model is null ? NotFound() : View(model);
    }
}