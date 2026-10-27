using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Account;
using PRN222_EWMS_Web.Services.Authentication;
using PRN222_EWMS_Web.ViewModels.Account;

namespace PRN222_EWMS_Web.Controllers.Account;

[Authorize]
public class AccountController(IAccountService accounts) : Controller
{
    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var model = await accounts.GetProfileAsync(CurrentUserId);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] EditProfileViewModel model)
    {
        if (ModelState.IsValid)
        {
            var errors = await accounts.UpdateProfileAsync(CurrentUserId, model);
            if (errors.Count == 0)
            {
                TempData["Success"] = "Đã cập nhật thông tin cá nhân.";
                return RedirectToAction(nameof(Profile));
            }
            AddErrors(errors, "Profile");
        }
        var page = await accounts.GetProfileAsync(CurrentUserId);
        if (page is null) return NotFound();
        page.Profile = model;
        return View(nameof(Profile), page);
    }

    [HttpGet]
    public IActionResult ChangePassword() => RedirectToAction(nameof(Profile), "Account", null, "change-password");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var session = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var result = await accounts.ChangePasswordAsync(CurrentUserId, model);
            if (result.Errors.Count == 0)
            {
                var identity = new ClaimsIdentity(User.Identity);
                foreach (var claim in identity.FindAll(AccountClaims.PasswordVersion).ToList())
                    identity.RemoveClaim(claim);
                identity.AddClaim(new Claim(AccountClaims.PasswordVersion, result.PasswordVersion!));
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity), session.Properties);
                TempData["Success"] = "Đổi mật khẩu thành công.";
                return RedirectToAction(nameof(Profile));
            }
            AddErrors(result.Errors, "Password");
        }
        var page = await accounts.GetProfileAsync(CurrentUserId);
        if (page is null) return NotFound();
        foreach (var field in new[] { "CurrentPassword", "NewPassword", "ConfirmPassword" })
            ModelState.SetModelValue($"Password.{field}", "", "");
        ViewData["PasswordError"] = true;
        return View(nameof(Profile), page);
    }

    private void AddErrors(Dictionary<string, string> errors, string prefix)
    {
        foreach (var error in errors)
            ModelState.AddModelError(string.IsNullOrEmpty(error.Key) ? "" : $"{prefix}.{error.Key}", error.Value);
    }
}
