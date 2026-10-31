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

    [HttpGet]
    public async Task<IActionResult> Edit(int id, [FromQuery] UserListFilter returnFilter)
    {
        var model = await users.GetEditAccountAsync(id, returnFilter);
        if (model is null) return NotFound();

        return View("~/Views/User/AccountEdit.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditAccountViewModel model)
    {
        if (id != model.UserId) return BadRequest();

        if (ModelState.IsValid)
        {
            var errors = await users.UpdateAccountAsync(id, model);
            foreach (var (field, message) in errors) ModelState.AddModelError(field, message);

            if (errors.Count == 0)
            {
                TempData["Success"] = "Đã cập nhật tài khoản.";
                return RedirectToDetails(id, model.ReturnFilter);
            }
        }

        if (!await users.PopulateEditOptionsAsync(model, id)) return NotFound();
        return View("~/Views/User/AccountEdit.cshtml", model);
    }

    private IActionResult RedirectToDetails(int id, UserListFilter filter) => RedirectToAction(nameof(Details), new { id, search = filter.Search, roleId = filter.RoleId, status = filter.Status, warehouseId = filter.WarehouseId, page = filter.Page });
}
