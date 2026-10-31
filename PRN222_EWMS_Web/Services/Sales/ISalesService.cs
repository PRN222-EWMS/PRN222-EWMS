using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Services.Sales;

public interface ISalesService
{
    Task<SalesDashboardViewModel> GetDashboardAsync(int userId);
    Task<SalesOrderListViewModel> GetOrdersAsync(SalesOrderListViewModel filter, int userId);
    Task<SalesOrderDetailsViewModel?> GetOrderAsync(long id, int userId);
    Task<CustomerListViewModel> GetCustomersAsync(CustomerListViewModel filter);
    Task<CustomerDetailsViewModel?> GetCustomerAsync(int id, int userId);
    Task<CustomerFormViewModel?> GetCustomerFormAsync(int id);
    Task<int> CreateCustomerAsync(CustomerFormViewModel model, int actorId);
    Task<bool> UpdateCustomerAsync(int id, CustomerFormViewModel model, int actorId);

    Task<SalesOrderFormViewModel> GetCreateFormAsync(int userId);
    Task<SalesOrderFormViewModel?> GetEditFormAsync(long id, int userId);
    Task<long> CreateOrderAsync(SalesOrderFormViewModel model, string action, int actorId);
    Task<bool> UpdateOrderAsync(long id, SalesOrderFormViewModel model, string action, int actorId);
    Task<List<ProductStockLookupDto>> GetAvailableProductsAsync(int warehouseId);
}
