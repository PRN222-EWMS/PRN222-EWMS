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
    private const int AccountListPageSize = 10;
    private static readonly string[] AccountStatuses = ["ACTIVE", "LOCKED", "INACTIVE"];

    public Task<List<User>> GetAllAsync() => users.GetAllAsync();

    public async Task<UserListViewModel> GetAccountListAsync(UserListFilter filter)
    {
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var roleId = filter.RoleId is > 0 ? filter.RoleId : null;
        var status = filter.Status is not null && AccountStatuses.Contains(filter.Status, StringComparer.Ordinal)
            ? filter.Status
            : null;
        var warehouseId = filter.WarehouseId is > 0 ? filter.WarehouseId : null;

        var page = await users.GetPageAsync(search, roleId, status, warehouseId, filter.Page, AccountListPageSize);
        var roles = await users.GetRolesAsync();
        var warehouses = await users.GetWarehousesAsync();

        return new UserListViewModel
        {
            Search = search,
            RoleId = roleId,
            Status = status,
            WarehouseId = warehouseId,
            Page = page.PageNumber,
            PageSize = AccountListPageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
            Accounts = page.Items.Select(account => new UserListItemViewModel
            {
                FullName = account.FullName,
                Email = account.Email,
                RoleName = account.RoleName,
                WarehouseName = account.WarehouseName,
                StatusLabel = GetAccountStatusLabel(account.Status),
                StatusClass = GetAccountStatusClass(account.Status),
                CreatedAt = account.CreatedAt
            }).ToList(),
            Roles = roles.Select(role => new SelectListItem(role.RoleName, role.RoleId.ToString())).ToList(),
            Warehouses = warehouses.Select(warehouse => new SelectListItem(
                warehouse.WarehouseName + (warehouse.Status == "ACTIVE" ? "" : " (Ngừng hoạt động)"),
                warehouse.WarehouseId.ToString())).ToList()
        };
    }

    private static string GetAccountStatusLabel(string status) => status switch
    {
        "ACTIVE" => "Đang hoạt động",
        "LOCKED" => "Bị khóa",
        "INACTIVE" => "Ngừng hoạt động",
        _ => status
    };

    private static string GetAccountStatusClass(string status) => status switch
    {
        "ACTIVE" => "active",
        "LOCKED" => "locked",
        "INACTIVE" => "inactive",
        _ => "unknown"
    };

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
