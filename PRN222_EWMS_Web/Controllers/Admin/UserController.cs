using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Admin;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
public class UserController(IUserService users) : Controller
{
    public async Task<IActionResult> Index() => View(await users.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateAccountViewModel();
        await users.PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAccountViewModel model)
    {
        if (ModelState.IsValid)
        {
            var errors = await users.CreateAsync(model);
            if (errors.Count == 0)
            {
                TempData["Success"] = "Tạo tài khoản thành công.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in errors) ModelState.AddModelError(error.Key, error.Value);
        }
        model.Password = model.ConfirmPassword = "";
        ModelState.SetModelValue(nameof(model.Password), "", "");
        ModelState.SetModelValue(nameof(model.ConfirmPassword), "", "");
        await users.PopulateOptionsAsync(model);
        return View(model);
    }
}
