using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using CustomerEntity = PRN222_EWMS_Web.Models.Customer;

namespace PRN222_EWMS_Web.Repositories.Customers;

public class CustomerRepository(EwmsDbContext db) : ICustomerRepository
{
    private const string CaseInsensitiveCollation = "Latin1_General_100_CI_AS";
    public IQueryable<SalesOrder> Orders => db.SalesOrders.AsNoTracking();

    public Task<CustomerEntity?> FindByIdAsync(int id) => db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == id);

    public async Task<(List<CustomerEntity> Items, int TotalCount, int Page, int TotalPages)> GetPageAsync(string? search, int page, int pageSize)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => EF.Functions.Collate(c.CustomerName, CaseInsensitiveCollation).Contains(term)
                || (c.Nickname != null && EF.Functions.Collate(c.Nickname, CaseInsensitiveCollation).Contains(term))
                || (c.Email != null && EF.Functions.Collate(c.Email, CaseInsensitiveCollation).Contains(term))
                || (c.Phone != null && EF.Functions.Collate(c.Phone, CaseInsensitiveCollation).Contains(term)));
        }
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await query.OrderByDescending(c => c.CustomerId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, totalCount, page, totalPages);
    }

    public async Task<CustomerSaveResult> SaveAsync(CustomerEntity values, int actorId, bool confirmDuplicatePhone)
    {
        if (actorId <= 0) throw new InvalidOperationException("Authenticated user required.");
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Re-read the current row and check email/phone under the same serializable
        // transaction as the write. This also covers concurrent application saves
        // without requiring an unauthorized schema/index change.
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var customer = values.CustomerId == 0 ? new CustomerEntity()
                : await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == values.CustomerId);
            if (customer is null)
            {
                errors[""] = "Không tìm thấy khách hàng.";
                return new(null, errors);
            }

            var emailChanged = values.CustomerId == 0 || !string.Equals(customer.Email?.Trim(), values.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged && values.Email != null && await db.Customers.AnyAsync(c => c.CustomerId != values.CustomerId
                && c.Email != null && EF.Functions.Collate(c.Email.Trim(), CaseInsensitiveCollation) == values.Email))
                errors["Email"] = "Email này đã được sử dụng bởi một khách hàng khác.";

            var phoneChanged = values.CustomerId == 0 || !string.Equals(customer.Phone?.Trim(), values.Phone, StringComparison.Ordinal);
            var duplicatePhone = phoneChanged && values.Phone != null && await db.Customers.AnyAsync(c => c.CustomerId != values.CustomerId
                && c.Phone != null && c.Phone.Trim() == values.Phone);
            if (duplicatePhone && !confirmDuplicatePhone)
                errors["ConfirmDuplicatePhone"] = "Số điện thoại này đã tồn tại ở một khách hàng khác. Vui lòng tích chọn xác nhận nếu muốn tiếp tục sử dụng số này.";
            if (errors.Count > 0) return new(null, errors, duplicatePhone);

            customer.CustomerName = values.CustomerName;
            customer.Nickname = values.Nickname;
            customer.Email = values.Email;
            customer.Address = values.Address;
            customer.Phone = values.Phone;
            if (values.CustomerId == 0) db.Customers.Add(customer);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}");
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(customer.CustomerId, errors, duplicatePhone);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            errors[""] = "Dữ liệu đang được cập nhật bởi thao tác khác. Vui lòng thử lưu lại.";
            return new(null, errors);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 })
        {
            errors[""] = "Dữ liệu đang được cập nhật bởi thao tác khác. Vui lòng thử lưu lại.";
            return new(null, errors);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            errors[""] = "Thông tin khách hàng trùng với dữ liệu vừa được lưu. Vui lòng kiểm tra và thử lại.";
            return new(null, errors);
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL"); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
