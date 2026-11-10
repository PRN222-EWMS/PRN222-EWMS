using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Repositories.WarehouseStaff;

namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;

[Authorize(Roles = "WAREHOUSE_STAFF")]
public class WarehouseTransferController(
    WarehouseTransferRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId))
            return Forbid();

        var orders = await repository.ListAsync(userId);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId))
            return Forbid();

        var detail = await repository.GetDetailAsync(userId, id);
        if (detail is null)
            return NotFound();

        return View(detail);
    }
}
