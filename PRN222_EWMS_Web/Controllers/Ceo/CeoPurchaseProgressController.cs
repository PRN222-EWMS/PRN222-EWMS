using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Ceo;
using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Controllers.Ceo;

[Authorize(Roles = "CEO")]
public class CeoPurchaseProgressController(ICeoPurchaseProgressService progress) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CeoPurchaseProgressFilter filter)
    {
        try { return View(await progress.GetPageAsync(filter)); }
        catch (ArgumentException) { return BadRequest(); }
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var model = await progress.GetDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }
}
