using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;

namespace PRN222_EWMS_Web.Services.Authentication;

public class PasswordResetService(
    IUserRepository users,
    IPasswordHasher<User> hasher,
    IDataProtectionProvider dataProtection,
    SmtpEmailSender emailSender,
    IConfiguration configuration,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private ITimeLimitedDataProtector Protector =>
        dataProtection.CreateProtector("EWMS.PasswordReset.v2").ToTimeLimitedDataProtector();

    public bool IsConfigured => emailSender.IsConfigured
        && Uri.TryCreate(configuration["PasswordReset:BaseUrl"], UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    public async Task SendResetLinkAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.Status != "ACTIVE") return;

        var version = AccountClaims.GetPasswordVersion(user.PasswordHash);
        var payload = Encoding.UTF8.GetBytes($"{user.UserId}:{version}");
        var token = WebEncoders.Base64UrlEncode(Protector.Protect(payload, TimeSpan.FromMinutes(1)));
        var baseUrl = configuration["PasswordReset:BaseUrl"]!.TrimEnd('/');
        var link = $"{baseUrl}/Authentication/ResetPassword?token={Uri.EscapeDataString(token)}";
        try { await emailSender.SendPasswordResetAsync(user.Email!, link, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not send a password reset email for user {UserId}", user.UserId);
        }
    }

    public async Task<bool> IsValidTokenAsync(string token)
        => await FindUserAsync(token) is not null;

    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        var user = await FindUserAsync(token);
        if (user is null) return false;
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        try { await users.SavePasswordResetAsync(); }
        catch (DbUpdateConcurrencyException) { return false; }
        return true;
    }

    private async Task<User?> FindUserAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return null;
        try
        {
            var payload = Encoding.UTF8.GetString(Protector.Unprotect(WebEncoders.Base64UrlDecode(token)));
            var parts = payload.Split(':', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var id)) return null;
            var user = await users.FindForPasswordResetAsync(id);
            if (user is null || user.Status != "ACTIVE") return null;
            var actualVersion = AccountClaims.GetPasswordVersion(user.PasswordHash);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(parts[1]), Encoding.ASCII.GetBytes(actualVersion)) ? user : null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
