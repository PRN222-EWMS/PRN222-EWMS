using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Supplier;

public class SupplierRepository(EwmsDbContext db) : ISupplierRepository
{
    public Task<Models.Supplier?> FindByIdAsync(int id) =>
        db.Suppliers.SingleOrDefaultAsync(s => s.SupplierId == id);

    public Task<bool> EmailExistsAsync(string email, int? excludeSupplierId = null)
    {
        var normalized = email.Trim().ToLower();
        return db.Suppliers.AnyAsync(s =>
            s.Email != null &&
            s.Email.ToLower() == normalized &&
            (!excludeSupplierId.HasValue || s.SupplierId != excludeSupplierId.Value));
    }

    public Task<bool> PhoneExistsAsync(string phone, int? excludeSupplierId = null)
    {
        var trimmed = phone.Trim();
        return db.Suppliers.AnyAsync(s =>
            s.Phone != null &&
            s.Phone.Trim() == trimmed &&
            (!excludeSupplierId.HasValue || s.SupplierId != excludeSupplierId.Value));
    }

    public async Task<(List<SupplierRecord> Items, int TotalCount, int PageNumber, int TotalPages)> GetPageAsync(
        string? search, string? status, int pageNumber, int pageSize)
    {
        var query = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                EF.Functions.Collate(s.SupplierName, "Latin1_General_100_CI_AS").Contains(term)
                || (s.Nickname != null && EF.Functions.Collate(s.Nickname, "Latin1_General_100_CI_AS").Contains(term))
                || (s.Email != null && EF.Functions.Collate(s.Email, "Latin1_General_100_CI_AS").Contains(term))
                || (s.Phone != null && EF.Functions.Collate(s.Phone, "Latin1_General_100_CI_AS").Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Status == status);
        }

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var normalizedPage = Math.Clamp(Math.Max(1, pageNumber), 1, Math.Max(1, totalPages));

        var items = await query
            .OrderByDescending(s => s.SupplierId)
            .Skip((normalizedPage - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierRecord
            {
                SupplierId = s.SupplierId,
                SupplierName = s.SupplierName,
                Nickname = s.Nickname,
                Address = s.Address,
                Phone = s.Phone,
                Email = s.Email,
                Status = s.Status
            })
            .ToListAsync();

        return (items, totalCount, normalizedPage, totalPages);
    }

    public async Task AddAsync(Models.Supplier supplier)
    {
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Models.Supplier supplier)
    {
        db.Suppliers.Update(supplier);
        await db.SaveChangesAsync();
    }
}
