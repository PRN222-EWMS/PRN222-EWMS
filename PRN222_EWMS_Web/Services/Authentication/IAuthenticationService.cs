using PRN222_EWMS_Web.Models;
using System.Security.Claims;

namespace PRN222_EWMS_Web.Services.Authentication;

public interface IAuthenticationService
{
    Task<User?> AuthenticateAsync(string email, string password);
    Task<User?> AuthenticateGoogleAsync(string subject, string email, string? hostedDomain);
    Task<User?> ValidateSessionAsync(ClaimsPrincipal? principal);
}
