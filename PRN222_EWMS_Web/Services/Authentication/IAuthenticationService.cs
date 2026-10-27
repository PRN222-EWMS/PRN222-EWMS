using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Services.Authentication;

public interface IAuthenticationService
{
    Task<User?> AuthenticateAsync(string username, string password);
}
