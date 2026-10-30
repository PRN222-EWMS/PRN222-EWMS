using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Admin;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
public class AccountListController(IUserService users) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UserListFilter filter)
    {
        var model = await users.GetAccountListAsync(filter);
        return View("~/Views/User/AccountList.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, [FromQuery] UserListFilter returnFilter)
    {
        var model = await users.GetAccountDetailsAsync(id);
        if (model is null) return NotFound();

        model.ReturnFilter = returnFilter;
        return View("~/Views/User/AccountDetails.cshtml", model);
    }
}
