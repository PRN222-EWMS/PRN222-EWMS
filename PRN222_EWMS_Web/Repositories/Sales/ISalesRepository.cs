using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Repositories.Sales;

public interface ISalesRepository
{
    IQueryable<SalesOrder> Orders { get; }
    IQueryable<Customer> Customers { get; }
    IQueryable<Warehouse> Warehouses { get; }
    IQueryable<Product> Products { get; }
    IQueryable<SupplySource> SupplySources { get; }
    IQueryable<Reservation> Reservations { get; }

    Task<Customer?> FindCustomerAsync(int id);
    Task<int> AddCustomerAsync(Customer customer, int actorId);
    Task SaveCustomerAsync(int actorId);

    Task<SalesOrder?> FindOrderForEditAsync(long id);
    Task<bool> HasAnyOutboundAsync(long salesOrderId);
    Task<string> GenerateNextSoCodeAsync();
    Task AddOrderAsync(SalesOrder order);
    Task RemoveOrderLinesAsync(IEnumerable<SalesOrderLine> lines);
    Task CloseActiveReservationsAsync(long salesOrderId);
    Task SaveChangesWithActorAsync(int actorId);
    Task<List<ProductStockLookupDto>> GetAvailableProductsAsync(int warehouseId);
}
