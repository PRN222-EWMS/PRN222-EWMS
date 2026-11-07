using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;

namespace PRN222_EWMS_Web.Services.Authentication;

public class AuthenticationService(IUserRepository users, IPasswordHasher<User> hasher) : IAuthenticationService
{
    public async Task<User?> AuthenticateGoogleAsync(string subject, string email, string? hostedDomain)
    {
        var account = await users.FindByGoogleSubjectAsync(subject);
        if (account is null)
        {
            var googleOwnsAddress = email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(hostedDomain)
                    && email.EndsWith("@" + hostedDomain, StringComparison.OrdinalIgnoreCase);
            if (!googleOwnsAddress) return null;

            account = await users.FindByEmailAsync(email);
            if (account is null || account.Status != "ACTIVE" || account.GoogleSubject is not null) return null;
            account.GoogleSubject = subject;
            try { await users.SaveAsync(); }
            catch (DbUpdateConcurrencyException) { return null; }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { return null; }
        }
        return account.Status == "ACTIVE" ? account : null;
    }

    public async Task<User?> ValidateSessionAsync(ClaimsPrincipal? principal)
    {
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(id, out var userId) ? await users.FindByIdAsync(userId) : null;
        if (user is null || user.Status != "ACTIVE"
            || user.Role.RoleName != principal?.FindFirstValue(ClaimTypes.Role)
            || (user.GoogleSubject ?? "") != principal?.FindFirstValue(AccountClaims.GoogleSubject)
            || AccountClaims.GetPasswordVersion(user.PasswordHash) != principal?.FindFirstValue(AccountClaims.PasswordVersion))
            return null;
        return user;
    }

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        var user = await users.FindByEmailAsync(email.Trim());
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
