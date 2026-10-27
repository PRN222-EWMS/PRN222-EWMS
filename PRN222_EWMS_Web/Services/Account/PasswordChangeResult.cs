namespace PRN222_EWMS_Web.Services.Account;

public record PasswordChangeResult(Dictionary<string, string> Errors, string? PasswordVersion = null);
