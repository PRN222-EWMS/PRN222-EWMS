using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Services.Authentication;

public static class AdminBootstrap
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled")) return;
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EwmsDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (await db.Users.AnyAsync(u => u.Role.RoleName == "ADMIN" && u.PasswordHash != "REPLACE_WITH_ASPNET_IDENTITY_HASH")) return;
        var username = configuration["BootstrapAdmin:Username"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 128)
            throw new InvalidOperationException("BootstrapAdmin requires a username and a password of 12–128 characters.");
        var role = await db.Roles.SingleAsync(r => r.RoleName == "ADMIN");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);
        if (user is not null && (user.RoleId != role.RoleId || user.PasswordHash != "REPLACE_WITH_ASPNET_IDENTITY_HASH"))
            throw new InvalidOperationException("BootstrapAdmin cannot replace an existing account password.");
        if (user is null)
        {
            if (await db.Users.AnyAsync(u => u.RoleId == role.RoleId))
                throw new InvalidOperationException("BootstrapAdmin cannot create another ADMIN account. Use the existing ADMIN username.");
            user = new User { Username = username, FullName = "Quản trị viên", RoleId = role.RoleId, Status = "ACTIVE", CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);
        }
        user.Status = "ACTIVE";
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
