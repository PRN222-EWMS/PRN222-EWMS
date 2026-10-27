using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using UserEntity = PRN222_EWMS_Web.Models.User;

namespace PRN222_EWMS_Web.Repositories.User;

public class UserRepository(EwmsDbContext db) : IUserRepository
{
    public Task<UserEntity?> FindByUsernameAsync(string username) => db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Username == username);
    public Task<UserEntity?> FindByIdAsync(int id) => db.Users.Include(u => u.Role).Include(u => u.Warehouse).SingleOrDefaultAsync(u => u.UserId == id);
    public Task<bool> UsernameExistsAsync(string username) => db.Users.AnyAsync(u => u.Username == username);
    public Task<bool> EmailExistsAsync(string email) => db.Users.AnyAsync(u => u.Email == email);
    public Task<bool> EmailExistsForOtherUserAsync(string email, int userId) => db.Users.AnyAsync(u => u.Email == email && u.UserId != userId);
    public Task<List<UserEntity>> GetAllAsync() => db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Warehouse).OrderByDescending(u => u.CreatedAt).ToListAsync();
    public Task<List<Role>> GetRolesAsync() => db.Roles.AsNoTracking().OrderBy(r => r.RoleId).ToListAsync();
    public Task<List<Warehouse>> GetActiveWarehousesAsync() => db.Warehouses.AsNoTracking().Where(w => w.Status == "ACTIVE").OrderBy(w => w.WarehouseName).ToListAsync();
    public async Task AddAsync(UserEntity user) { db.Users.Add(user); await db.SaveChangesAsync(); }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
