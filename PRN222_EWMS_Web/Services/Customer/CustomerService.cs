using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Repositories.Customers;
using PRN222_EWMS_Web.ViewModels.Customer;
using SalesOrderRowViewModel = PRN222_EWMS_Web.ViewModels.Sales.SalesOrderRowViewModel;
using CustomerEntity = PRN222_EWMS_Web.Models.Customer;
using CustomerDetailsModel = PRN222_EWMS_Web.ViewModels.Customer.CustomerDetailsViewModel;
using CustomerListModel = PRN222_EWMS_Web.ViewModels.Customer.CustomerListViewModel;
using CustomerRowModel = PRN222_EWMS_Web.ViewModels.Customer.CustomerRowViewModel;

namespace PRN222_EWMS_Web.Services.Customers;

public class CustomerService(ICustomerRepository repo) : ICustomerService
{
    private const int PageSize = 10;

    public async Task<CustomerListModel> GetCustomerListAsync(CustomerListFilter filter)
    {
        var search = Clean(filter.Search);
        var (items, count, page, pages) = await repo.GetPageAsync(search, Math.Max(1, filter.Page), PageSize);
        return new CustomerListModel
        {
            Search = search, Page = page, PageSize = PageSize, TotalCount = count, TotalPages = pages,
            Items = items.Select(c => new CustomerRowModel
            { Id = c.CustomerId, CustomerName = c.CustomerName, Nickname = c.Nickname, Email = c.Email, Address = c.Address, Phone = c.Phone }).ToList()
        };
    }

    public async Task<CustomerDetailsModel?> GetCustomerDetailsAsync(int customerId, int userId, CustomerListFilter? returnFilter = null)
    {
        var customer = await repo.FindByIdAsync(customerId);
        if (customer is null) return null;
        var orders = await repo.Orders.Where(o => o.CustomerId == customerId).OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.SalesOrderId)
            .Select(o => new SalesOrderRowViewModel
            {
                Id = o.SalesOrderId, Code = o.Socode, CustomerName = o.CustomerNameSnapshot ?? o.Customer.CustomerName,
                WarehouseName = o.Warehouse.WarehouseName, CreatedByName = o.CreatedByUser.FullName,
                CreatedByUserId = o.CreatedByUserId, OrderDate = o.OrderDate.ToDateTime(TimeOnly.MinValue),
                Status = o.Status, HasShortage = o.HasShortage,
                CanEdit = o.CreatedByUserId == userId && (o.Status == "DRAFT" || o.Status == "SUBMITTED") && !o.Outbounds.Any()
            }).ToListAsync();
        return new CustomerDetailsModel
        {
            Id = customer.CustomerId, CustomerName = customer.CustomerName, Nickname = customer.Nickname,
            Email = customer.Email, Address = customer.Address, Phone = customer.Phone, Orders = orders,
            ReturnFilter = NormalizeFilter(returnFilter)
        };
    }

    public async Task<EditCustomerViewModel?> GetEditCustomerAsync(int customerId, CustomerListFilter? returnFilter = null)
    {
        var customer = await repo.FindByIdAsync(customerId);
        return customer is null ? null : new EditCustomerViewModel
        {
            Id = customer.CustomerId, CustomerName = customer.CustomerName, Nickname = customer.Nickname,
            Email = customer.Email, Address = customer.Address, Phone = customer.Phone,
            ReturnFilter = NormalizeFilter(returnFilter)
        };
    }

    public async Task<(int? CustomerId, Dictionary<string, string> Errors)> CreateAsync(CreateCustomerViewModel model, int actorId)
    {
        var errors = NormalizeAndValidate(model);
        if (errors.Count > 0) return (null, errors);
        var result = await repo.SaveAsync(ToEntity(model), actorId, model.ConfirmDuplicatePhone);
        model.HasDuplicatePhoneWarning = result.HasDuplicatePhoneWarning;
        return (result.CustomerId, result.Errors);
    }

    public async Task<Dictionary<string, string>> UpdateAsync(int customerId, EditCustomerViewModel model, int actorId)
    {
        var errors = NormalizeAndValidate(model);
        if (errors.Count > 0) return errors;
        var values = ToEntity(model);
        values.CustomerId = customerId;
        var result = await repo.SaveAsync(values, actorId, model.ConfirmDuplicatePhone);
        model.HasDuplicatePhoneWarning = result.HasDuplicatePhoneWarning;
        return result.Errors;
    }

    private static Dictionary<string, string> NormalizeAndValidate(CreateCustomerViewModel model)
    {
        model.CustomerName = Clean(model.CustomerName) ?? "";
        model.Nickname = Clean(model.Nickname);
        model.Email = Clean(model.Email);
        model.Address = Clean(model.Address);
        model.Phone = Clean(model.Phone);
        model.HasDuplicatePhoneWarning = false;
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
            foreach (var field in result.MemberNames.DefaultIfEmpty(""))
                errors.TryAdd(field, result.ErrorMessage ?? "Dữ liệu không hợp lệ.");
        return errors;
    }

    private static CustomerEntity ToEntity(CreateCustomerViewModel model) => new()
    { CustomerName = model.CustomerName, Nickname = model.Nickname, Email = model.Email, Address = model.Address, Phone = model.Phone };

    private static CustomerListFilter NormalizeFilter(CustomerListFilter? filter) => new()
    { Search = Clean(filter?.Search), Page = Math.Max(1, filter?.Page ?? 1) };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
