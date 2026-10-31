using PRN222_EWMS_Web.Models;
using UserEntity = PRN222_EWMS_Web.Models.User;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.User;

public interface IUserRepository
{
    Task<UserEntity?> FindByUsernameAsync(string username);
    Task<UserEntity?> FindByIdAsync(int id);
    Task<bool> UsernameExistsAsync(string username);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> EmailExistsForOtherUserAsync(string email, int userId);
    Task<List<UserEntity>> GetAllAsync();
    Task<(List<UserListRecord> Items, int TotalCount, int PageNumber, int TotalPages)> GetPageAsync(
        string? search, int? roleId, string? status, int? warehouseId, int pageNumber, int pageSize);
    Task<List<Role>> GetRolesAsync();
    Task<List<WarehouseEntity>> GetWarehousesAsync();
    Task<List<WarehouseEntity>> GetActiveWarehousesAsync();
    Task AddAsync(UserEntity user);
    Task SaveAsync();
}
