using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.Services.Authentication;
using PRN222_EWMS_Web.ViewModels.Account;

namespace PRN222_EWMS_Web.Services.Account;

public class AccountService(IUserRepository users, IPasswordHasher<User> hasher) : IAccountService
{
    public async Task<ProfileViewModel?> GetProfileAsync(int userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.Status != "ACTIVE") return null;
        return new ProfileViewModel
        {
            Username = user.Username,
            RoleName = user.Role.RoleName,
            WarehouseName = user.Warehouse?.WarehouseName,
            Profile = new EditProfileViewModel { FullName = user.FullName, Email = user.Email, Phone = user.Phone }
        };
    }

    public async Task<Dictionary<string, string>> UpdateProfileAsync(int userId, EditProfileViewModel model)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.Status != "ACTIVE") return new() { [""] = "Tài khoản không còn hoạt động." };
        var email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        if (email is not null && await users.EmailExistsForOtherUserAsync(email, userId))
            return new() { [nameof(model.Email)] = "Email đã được sử dụng." };
        user.FullName = model.FullName.Trim();
        user.Email = email;
        user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        try { await users.SaveAsync(); }
        catch (DbUpdateConcurrencyException)
        { return new() { [""] = "Thông tin đã thay đổi. Vui lòng tải lại trang và thử lại." }; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return new() { [nameof(model.Email)] = "Email đã được sử dụng." }; }
        return new();
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(int userId, ChangePasswordViewModel model)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.Status != "ACTIVE") return new(new() { [""] = "Tài khoản không còn hoạt động." });
        if (!VerifyPassword(user, model.CurrentPassword))
            return new(new() { [nameof(model.CurrentPassword)] = "Mật khẩu hiện tại không đúng." });
        if (model.NewPassword == model.CurrentPassword)
            return new(new() { [nameof(model.NewPassword)] = "Mật khẩu mới phải khác mật khẩu hiện tại." });
        user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
        try { await users.SaveAsync(); }
        catch (DbUpdateConcurrencyException)
        { return new(new() { [""] = "Tài khoản đã thay đổi. Vui lòng tải lại trang và thử lại." }); }
        return new(new(), AccountClaims.GetPasswordVersion(user.PasswordHash));
    }

    private bool VerifyPassword(User user, string password)
    {
        try { return hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed; }
        catch (FormatException) { return false; }
    }
}
