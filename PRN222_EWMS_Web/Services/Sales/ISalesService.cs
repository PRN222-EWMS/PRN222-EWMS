using PRN222_EWMS_Web.Models;
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
    Task<(bool Success, Customer? Customer, string? ErrorMessage)> QuickCreateCustomerAsync(CustomerFormViewModel model, int actorId);
    Task<bool> UpdateCustomerAsync(int id, CustomerFormViewModel model, int actorId);

    Task<SalesOrderFormViewModel> GetCreateFormAsync(int userId);
    Task<SalesOrderFormViewModel?> GetEditFormAsync(long id, int userId);
    Task<long> CreateOrderAsync(SalesOrderFormViewModel model, string action, int actorId);
    Task<bool> UpdateOrderAsync(long id, SalesOrderFormViewModel model, string action, int actorId);
    Task<(bool Success, string Message)> CancelOrderAsync(long id, int actorId, string actorRole, string? cancelReason = null);
    Task<List<ProductStockLookupDto>> GetAvailableProductsAsync(int warehouseId, DateOnly? requestedDate = null);
    Task<List<SalesSourceOption>> GetSourceOptionsAsync(int warehouseId, DateOnly? requestedDate);
    Task<SalesOrderFormViewModel?> GetReplacementFormAsync(long id, int userId);
    Task AddReplacementAsync(long id, SalesOrderFormViewModel form, int actorId);
}
