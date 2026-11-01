using PRN222_EWMS_Web.Models;
using CustomerEntity = PRN222_EWMS_Web.Models.Customer;

namespace PRN222_EWMS_Web.Repositories.Customers;

public interface ICustomerRepository
{
    IQueryable<SalesOrder> Orders { get; }
    Task<CustomerEntity?> FindByIdAsync(int id);
    Task<(List<CustomerEntity> Items, int TotalCount, int Page, int TotalPages)> GetPageAsync(string? search, int page, int pageSize);
    Task<CustomerSaveResult> SaveAsync(CustomerEntity values, int actorId, bool confirmDuplicatePhone);
}

public record CustomerSaveResult(int? CustomerId, Dictionary<string, string> Errors, bool HasDuplicatePhoneWarning = false);
