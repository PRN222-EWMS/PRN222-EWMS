using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public class UserService(IUserRepository users, IPasswordHasher<User> hasher) : IUserService
{
    public Task<List<User>> GetAllAsync() => users.GetAllAsync();

    public async Task PopulateOptionsAsync(CreateAccountViewModel model)
    {
        model.Roles = (await users.GetRolesAsync())
            .Where(r => r.RoleName != "ADMIN")
            .Select(r => new SelectListItem(r.RoleName, r.RoleId.ToString())).ToList();
        model.Warehouses = (await users.GetActiveWarehousesAsync()).Select(w => new SelectListItem(w.WarehouseName, w.WarehouseId.ToString())).ToList();
    }

    public async Task<Dictionary<string, string>> CreateAsync(CreateAccountViewModel model)
    {
        var errors = new Dictionary<string, string>();
        var username = model.Username.Trim();
        var email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        var role = (await users.GetRolesAsync()).SingleOrDefault(r => r.RoleId == model.RoleId);
        if (role is null) errors[nameof(model.RoleId)] = "Vai trò không hợp lệ.";
        if (role?.RoleName == "ADMIN") errors[nameof(model.RoleId)] = "Không được tạo thêm tài khoản ADMIN.";
        var isWarehouseStaff = role?.RoleName == "WAREHOUSE_STAFF";
        if (!isWarehouseStaff) model.WarehouseId = null;
        if (isWarehouseStaff && model.WarehouseId is null)
            errors[nameof(model.WarehouseId)] = "Nhân viên kho phải được phân công kho làm việc.";
        if (isWarehouseStaff && model.WarehouseId.HasValue && !(await users.GetActiveWarehousesAsync()).Any(w => w.WarehouseId == model.WarehouseId))
            errors[nameof(model.WarehouseId)] = "Kho không tồn tại hoặc đã ngừng hoạt động.";
        if (await users.UsernameExistsAsync(username)) errors[nameof(model.Username)] = "Tên đăng nhập đã tồn tại.";
        if (email is not null && await users.EmailExistsAsync(email)) errors[nameof(model.Email)] = "Email đã được sử dụng.";
        if (errors.Count > 0) return errors;
        var user = new User
        {
            Username = username, FullName = model.FullName.Trim(), Email = email,
            Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
            RoleId = model.RoleId, WarehouseId = model.WarehouseId, Status = "ACTIVE", CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, model.Password);
        try { await users.AddAsync(user); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { errors[""] = "Tên đăng nhập hoặc email đã được sử dụng. Vui lòng kiểm tra lại."; }
        return errors;
    }
}
