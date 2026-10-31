using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Supplier;

public record SupplierRecord
{
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = null!;
    public string? Nickname { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string Status { get; init; } = null!;
}

public interface ISupplierRepository
{
    Task<Models.Supplier?> FindByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email, int? excludeSupplierId = null);
    Task<bool> PhoneExistsAsync(string phone, int? excludeSupplierId = null);
    Task<(List<SupplierRecord> Items, int TotalCount, int PageNumber, int TotalPages)> GetPageAsync(
        string? search, string? status, int pageNumber, int pageSize);
    Task AddAsync(Models.Supplier supplier);
    Task UpdateAsync(Models.Supplier supplier);
}
