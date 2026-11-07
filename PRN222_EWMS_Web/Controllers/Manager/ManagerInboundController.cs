using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Manager;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Controllers.Manager;

[Authorize(Roles = "MANAGER")]
public class ManagerInboundController(IManagerInboundService inbounds) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ManagerInboundFilter filter)
    {
        try { return View(await inbounds.GetListAsync(filter)); }
        catch (ArgumentException) { return BadRequest(); }
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var model = await inbounds.GetDetailAsync(id);
        return model is null ? NotFound() : View(model);
    }
}
