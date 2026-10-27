using PRN222_EWMS_Web.ViewModels.Account;

namespace PRN222_EWMS_Web.Services.Account;

public interface IAccountService
{
    Task<ProfileViewModel?> GetProfileAsync(int userId);
    Task<Dictionary<string, string>> UpdateProfileAsync(int userId, EditProfileViewModel model);
    Task<PasswordChangeResult> ChangePasswordAsync(int userId, ChangePasswordViewModel model);
}
