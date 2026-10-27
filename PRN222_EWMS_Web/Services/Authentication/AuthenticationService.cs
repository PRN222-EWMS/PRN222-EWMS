using Microsoft.AspNetCore.Identity;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;

namespace PRN222_EWMS_Web.Services.Authentication;

public class AuthenticationService(IUserRepository users, IPasswordHasher<User> hasher) : IAuthenticationService
{
    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await users.FindByUsernameAsync(username.Trim());
        if (user is null || user.Status != "ACTIVE") return null;
        PasswordVerificationResult result;
        try { result = hasher.VerifyHashedPassword(user, user.PasswordHash, password); }
        catch (FormatException) { return null; }
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await users.SaveAsync();
        }
        return user;
    }
}
