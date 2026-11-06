using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.ViewModels.Authentication;
using PRN222_EWMS_Web.Services.Authentication;
using LoginService = PRN222_EWMS_Web.Services.Authentication.IAuthenticationService;

namespace PRN222_EWMS_Web.Controllers.Authentication;

public class AuthenticationController(LoginService authentication, IUserRepository users, PasswordResetService passwordReset, IConfiguration configuration, ILogger<AuthenticationController> logger) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null, bool googleError = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (googleError) TempData["GoogleError"] = "Không thể xác thực tài khoản Google. Vui lòng thử lại.";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        PRN222_EWMS_Web.Models.User? user;
        try { user = await authentication.AuthenticateAsync(model.Email, model.Password); }
        catch (Exception ex) when (ex is TimeoutException or SqlException { Number: -2 })
        {
            logger.LogError(ex, "Database timed out while signing in with email");
            ModelState.AddModelError("", "Cơ sở dữ liệu đang phản hồi chậm. Vui lòng thử đăng nhập lại sau ít phút.");
            return View(model);
        }
        if (user is null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");
            return View(model);
        }
        await SignInUserAsync(user, model.RememberMe);
        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        if (!GoogleConfigured())
        {
            TempData["GoogleError"] = "Đăng nhập Google chưa được cấu hình.";
            return RedirectToAction(nameof(Login));
        }
        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(GoogleCallback)) };
        if (Url.IsLocalUrl(returnUrl)) properties.Items["returnUrl"] = returnUrl;
        return Challenge(properties, "Google");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        if (!passwordReset.IsConfigured)
        {
            ModelState.AddModelError("", "Chức năng gửi email chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }
        try { await passwordReset.SendResetLinkAsync(model.Email, cancellationToken); }
        catch (SqlException ex) when (ex.Number == -2)
        {
            logger.LogError(ex, "Database timed out while requesting a password reset");
            return PasswordResetUnavailable();
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Database timed out while requesting a password reset");
            return PasswordResetUnavailable();
        }
        return View("ForgotPasswordSent");
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        try
        {
            if (token is null || !await passwordReset.IsValidTokenAsync(token)) return View("ResetPasswordInvalid");
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            logger.LogError(ex, "Database timed out while validating a password reset link");
            return PasswordResetUnavailable();
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Database timed out while validating a password reset link");
            return PasswordResetUnavailable();
        }
        return View(new ResetPasswordViewModel { Token = token });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) return View(model);
        try
        {
            if (!await passwordReset.ResetPasswordAsync(model.Token, model.NewPassword)) return View("ResetPasswordInvalid");
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            logger.LogError(ex, "Database timed out while resetting a password");
            return PasswordResetUnavailable();
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Database timed out while resetting a password");
            return PasswordResetUnavailable();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: -2 })
        {
            logger.LogError(ex, "Database timed out while saving a new password");
            return PasswordResetUnavailable();
        }
        TempData["Success"] = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult PasswordResetUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("ResetPasswordUnavailable");
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> GoogleCallback()
    {
        var external = await HttpContext.AuthenticateAsync("GoogleExternal");
        await HttpContext.SignOutAsync("GoogleExternal");
        var subject = external.Principal?.FindFirstValue("google:sub");
        var googleEmail = external.Principal?.FindFirstValue("google:email");
        if (!external.Succeeded || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(googleEmail))
        {
            TempData["GoogleError"] = "Không thể xác thực tài khoản Google.";
            return RedirectToAction(nameof(Login));
        }

        try
        {
        var account = await users.FindByGoogleSubjectAsync(subject);
        if (account is null)
        {
            var hostedDomain = external.Principal?.FindFirstValue("google:hd");
            var googleOwnsAddress = googleEmail.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(hostedDomain)
                    && googleEmail.EndsWith("@" + hostedDomain, StringComparison.OrdinalIgnoreCase);
            if (googleOwnsAddress)
            {
                account = await users.FindByEmailAsync(googleEmail);
                if (account is not null && account.Status == "ACTIVE" && account.GoogleSubject is null)
                {
                    account.GoogleSubject = subject;
                    try { await users.SaveAsync(); }
                    catch (DbUpdateConcurrencyException)
                    {
                        account = null;
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
                    {
                        account = null;
                    }
                }
                else account = null;
            }
        }
        if (account is null || account.Status != "ACTIVE")
        {
            TempData["GoogleError"] = "Không tìm thấy tài khoản EWMS đang hoạt động với email Google này, hoặc tài khoản đã gắn với Google khác.";
            return RedirectToAction(nameof(Login));
        }
        await SignInUserAsync(account, false);
        var returnUrl = external.Properties?.Items.TryGetValue("returnUrl", out var destination) == true ? destination : null;
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
        }
        catch (Exception ex) when (ex is TimeoutException or SqlException { Number: -2 }
            or DbUpdateException { InnerException: SqlException { Number: -2 } })
        {
            logger.LogError(ex, "Database timed out while signing in with Google");
            TempData["GoogleError"] = "Cơ sở dữ liệu đang phản hồi chậm. Vui lòng thử đăng nhập lại sau ít phút.";
            return RedirectToAction(nameof(Login));
        }
    }

    private bool GoogleConfigured() =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    private Task SignInUserAsync(PRN222_EWMS_Web.Models.User user, bool rememberMe)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.RoleName),
            new Claim(AccountClaims.GoogleSubject, user.GoogleSubject ?? ""),
            new Claim(AccountClaims.PasswordVersion, AccountClaims.GetPasswordVersion(user.PasswordHash))
        };
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = rememberMe });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
