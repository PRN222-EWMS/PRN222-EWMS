namespace PRN222_EWMS_Web.Services.Authentication;

public interface IPasswordResetService
{
    bool IsConfigured { get; }
    Task SendResetLinkAsync(string email, CancellationToken cancellationToken);
    Task<bool> IsValidTokenAsync(string token);
    Task<bool> ResetPasswordAsync(string token, string newPassword);
}
