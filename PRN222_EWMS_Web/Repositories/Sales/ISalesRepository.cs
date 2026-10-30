using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Sales;

public interface ISalesRepository
{
    IQueryable<SalesOrder> Orders { get; }
    IQueryable<Customer> Customers { get; }
    IQueryable<Warehouse> Warehouses { get; }
    Task<Customer?> FindCustomerAsync(int id);
    Task<int> AddCustomerAsync(Customer customer, int actorId);
    Task SaveCustomerAsync(int actorId);
}
