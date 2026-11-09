using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Manager;

namespace PRN222_EWMS_Web.Controllers.Manager;

[Authorize(Roles = "MANAGER")]
public class ManagerTransferController(IManagerTransferService transfers)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var model = await transfers.GetListAsync(search, status, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var detail = await transfers.GetDetailAsync(id);
        return detail is null ? NotFound() : View(detail);
    }
}
