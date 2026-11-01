using PRN222_EWMS_Web.ViewModels.Customer;

namespace PRN222_EWMS_Web.Services.Customers;

public interface ICustomerService
{
    Task<CustomerListViewModel> GetCustomerListAsync(CustomerListFilter filter);
    Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(int customerId, int userId, CustomerListFilter? returnFilter = null);
    Task<EditCustomerViewModel?> GetEditCustomerAsync(int customerId, CustomerListFilter? returnFilter = null);
    Task<(int? CustomerId, Dictionary<string, string> Errors)> CreateAsync(CreateCustomerViewModel model, int actorId);
    Task<Dictionary<string, string>> UpdateAsync(int customerId, EditCustomerViewModel model, int actorId);
}
