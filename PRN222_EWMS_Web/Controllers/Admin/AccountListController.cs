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
}
