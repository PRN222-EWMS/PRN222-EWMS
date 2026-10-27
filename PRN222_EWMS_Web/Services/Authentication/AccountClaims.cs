using System.Security.Cryptography;
using System.Text;

namespace PRN222_EWMS_Web.Services.Authentication;

public static class AccountClaims
{
    public const string PasswordVersion = "ewms:password-version";

    public static string GetPasswordVersion(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
}
