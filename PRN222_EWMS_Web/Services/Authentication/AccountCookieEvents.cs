using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using PRN222_EWMS_Web.Repositories.User;

namespace PRN222_EWMS_Web.Services.Authentication;

public class AccountCookieEvents(IUserRepository users) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(id, out var userId) ? await users.FindByIdAsync(userId) : null;
        if (user is null || user.Status != "ACTIVE"
            || user.Role.RoleName != context.Principal?.FindFirstValue(ClaimTypes.Role)
            || AccountClaims.GetPasswordVersion(user.PasswordHash) != context.Principal?.FindFirstValue(AccountClaims.PasswordVersion))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        if (user.FullName != context.Principal?.Identity?.Name && context.Principal?.Identity is ClaimsIdentity identity)
        {
            var nameClaim = identity.FindFirst(ClaimTypes.Name);
            if (nameClaim is not null) identity.RemoveClaim(nameClaim);
            identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName));
            context.ShouldRenew = true;
        }
    }
}
