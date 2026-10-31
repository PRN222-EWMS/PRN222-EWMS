using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using UserEntity = PRN222_EWMS_Web.Models.User;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.User;

public class UserRepository(EwmsDbContext db) : IUserRepository
{
    public Task<UserEntity?> FindByUsernameAsync(string username) => db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Username == username);
    public Task<UserEntity?> FindByIdAsync(int id) => db.Users.Include(u => u.Role).Include(u => u.Warehouse).SingleOrDefaultAsync(u => u.UserId == id);
    public Task<bool> UsernameExistsAsync(string username) => db.Users.AnyAsync(u => u.Username == username);
    public Task<bool> EmailExistsAsync(string email) => db.Users.AnyAsync(u => u.Email == email);
    public Task<bool> EmailExistsForOtherUserAsync(string email, int userId) => db.Users.AnyAsync(u => u.Email == email && u.UserId != userId);
    public Task<List<UserEntity>> GetAllAsync() => db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Warehouse).OrderByDescending(u => u.CreatedAt).ToListAsync();
    public async Task<(List<UserListRecord> Items, int TotalCount, int PageNumber, int TotalPages)> GetPageAsync(
        string? search, int? roleId, string? status, int? warehouseId, int pageNumber, int pageSize)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search!;
            query = query.Where(u => EF.Functions.Collate(u.FullName, "Latin1_General_100_CI_AS").Contains(term)
                || (u.Email != null && EF.Functions.Collate(u.Email, "Latin1_General_100_CI_AS").Contains(term)));
        }
        if (roleId.HasValue) query = query.Where(u => u.RoleId == roleId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(u => u.Status == status);
        if (warehouseId.HasValue) query = query.Where(u => u.WarehouseId == warehouseId.Value);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var normalizedPage = Math.Clamp(Math.Max(1, pageNumber), 1, Math.Max(1, totalPages));
        var items = await query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.UserId)
            .Skip((normalizedPage - 1) * pageSize).Take(pageSize)
            .Select(u => new UserListRecord
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                RoleName = u.Role.RoleName,
                WarehouseName = u.Warehouse == null ? null : u.Warehouse.WarehouseName,
                Status = u.Status,
                CreatedAt = u.CreatedAt
            }).ToListAsync();
        return (items, totalCount, normalizedPage, totalPages);
    }
    public Task<List<Role>> GetRolesAsync() => db.Roles.AsNoTracking().OrderBy(r => r.RoleId).ToListAsync();
    public Task<List<WarehouseEntity>> GetWarehousesAsync() => db.Warehouses.AsNoTracking().OrderBy(w => w.WarehouseName).ToListAsync();
    public Task<List<WarehouseEntity>> GetActiveWarehousesAsync() => db.Warehouses.AsNoTracking().Where(w => w.Status == "ACTIVE").OrderBy(w => w.WarehouseName).ToListAsync();
    public async Task AddAsync(UserEntity user) { db.Users.Add(user); await db.SaveChangesAsync(); }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
