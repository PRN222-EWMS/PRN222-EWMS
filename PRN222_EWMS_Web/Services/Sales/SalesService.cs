using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Sales;
using PRN222_EWMS_Web.ViewModels.Sales;

namespace PRN222_EWMS_Web.Services.Sales;

public class SalesService(ISalesRepository repository) : ISalesService
{
    private static Expression<Func<SalesOrder, SalesOrderRowViewModel>> Row(int userId) => o => new SalesOrderRowViewModel
    {
        Id = o.SalesOrderId, Code = o.Socode,
        CustomerName = o.CustomerNameSnapshot ?? o.Customer.CustomerName,
        WarehouseName = o.Warehouse.WarehouseName, CreatedByName = o.CreatedByUser.FullName,
        CreatedByUserId = o.CreatedByUserId, OrderDate = o.OrderDate.ToDateTime(TimeOnly.MinValue),
        Status = o.Status, HasShortage = o.HasShortage,
        CanEdit = o.CreatedByUserId == userId && (o.Status == "DRAFT" || o.Status == "SUBMITTED") && !o.Outbounds.Any()
    };

    public async Task<SalesDashboardViewModel> GetDashboardAsync(int userId)
    {
        var counts = await repository.Orders.GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        return new SalesDashboardViewModel
        {
            TotalOrders = counts.Sum(x => x.Count),
            WaitingOrders = counts.Where(x => x.Status == "SUBMITTED").Sum(x => x.Count),
            PartialOrders = counts.Where(x => x.Status == "PARTIALLY_FULFILLED").Sum(x => x.Count),
            FulfilledOrders = counts.Where(x => x.Status == "FULFILLED").Sum(x => x.Count),
            ShortageOrders = await repository.Orders.CountAsync(x => x.HasShortage && (x.Status == "SUBMITTED" || x.Status == "PARTIALLY_FULFILLED")),
            CustomerCount = await repository.Customers.CountAsync(),
            RecentOrders = await repository.Orders.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.SalesOrderId).Take(5).Select(Row(userId)).ToListAsync()
        };
    }

    public async Task<SalesOrderListViewModel> GetOrdersAsync(SalesOrderListViewModel filter, int userId)
    {
        var query = repository.Orders;
        filter.Search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(x => x.Socode.Contains(search) || (x.CustomerNameSnapshot ?? x.Customer.CustomerName).Contains(search));
        }
        if (!string.IsNullOrEmpty(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        if (filter.WarehouseId.HasValue) query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        if (filter.Mine) query = query.Where(x => x.CreatedByUserId == userId);
        if (filter.From.HasValue) query = query.Where(x => x.OrderDate >= DateOnly.FromDateTime(filter.From.Value));
        if (filter.To.HasValue) query = query.Where(x => x.OrderDate <= DateOnly.FromDateTime(filter.To.Value));
        filter.TotalCount = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, filter.TotalPages);
        filter.Items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.SalesOrderId)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(Row(userId)).ToListAsync();
        filter.Warehouses = await repository.Warehouses.OrderBy(x => x.WarehouseName)
            .Select(x => new SelectListItem(x.WarehouseName, x.WarehouseId.ToString())).ToListAsync();
        return filter;
    }

    public Task<SalesOrderDetailsViewModel?> GetOrderAsync(long id, int userId) => repository.Orders
        .Where(o => o.SalesOrderId == id).Select(o => new SalesOrderDetailsViewModel
        {
            Id = o.SalesOrderId, Code = o.Socode, CustomerName = o.CustomerNameSnapshot ?? o.Customer.CustomerName,
            // Once a snapshot exists, a null snapshot field means no contact was recorded then.
            CustomerPhone = o.CustomerNameSnapshot != null ? o.CustomerPhoneSnapshot : o.Customer.Phone,
            CustomerAddress = o.CustomerNameSnapshot != null ? o.CustomerAddressSnapshot : o.Customer.Address,
            WarehouseName = o.Warehouse.WarehouseName, CreatedByName = o.CreatedByUser.FullName,
            CreatedByUserId = o.CreatedByUserId, OrderDate = o.OrderDate.ToDateTime(TimeOnly.MinValue), Status = o.Status,
            HasShortage = o.HasShortage, RequestedDate = o.RequestedDate.HasValue ? o.RequestedDate.Value.ToDateTime(TimeOnly.MinValue) : null, SubmittedAt = o.SubmittedAt,
            ClosedAt = o.ClosedAt, CloseReason = o.CloseReason, Note = o.Note,
            CanEdit = o.CreatedByUserId == userId && (o.Status == "DRAFT" || o.Status == "SUBMITTED") && !o.Outbounds.Any(),
            Lines = o.SalesOrderLines.OrderBy(l => l.SolineId).Select(l => new SalesOrderLineViewModel
            {
                ProductCode = l.Product.ProductCode, ProductName = l.Product.ProductName,
                RequestedQuantity = l.RequestedQuantity, CancelledQuantity = l.CancelledQuantity,
                IssuedQuantity = l.OutboundItemSolines.Count(i => i.IsActive && i.Outbound.Status == "DONE")
            }).ToList(),
            Outbounds = o.Outbounds.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.OutboundId).Select(x => new OutboundSummaryViewModel
            {
                Id = x.OutboundId, Code = x.IssueCode, Status = x.Status, IssueDate = x.IssueDate.ToDateTime(TimeOnly.MinValue),
                CompletedAt = x.CompletedAt, Quantity = x.OutboundItemOutbounds.Count(i => i.IsActive)
            }).ToList()
        }).SingleOrDefaultAsync();

    public async Task<CustomerListViewModel> GetCustomersAsync(CustomerListViewModel filter)
    {
        var query = repository.Customers;
        filter.Search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(x => x.CustomerName.Contains(search) || (x.Phone != null && x.Phone.Contains(search)));
        }
        filter.TotalCount = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, filter.TotalPages);
        filter.Items = await query.OrderBy(x => x.CustomerName).ThenBy(x => x.CustomerId)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new CustomerRowViewModel { Id = x.CustomerId, CustomerName = x.CustomerName, Address = x.Address, Phone = x.Phone }).ToListAsync();
        return filter;
    }

    public async Task<CustomerDetailsViewModel?> GetCustomerAsync(int id, int userId)
    {
        var customer = await repository.Customers.Where(x => x.CustomerId == id).Select(x => new CustomerDetailsViewModel
        { Id = x.CustomerId, CustomerName = x.CustomerName, Address = x.Address, Phone = x.Phone }).SingleOrDefaultAsync();
        if (customer is null) return null;
        customer.Orders = await repository.Orders.Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.SalesOrderId).Select(Row(userId)).ToListAsync();
        return customer;
    }

    public Task<CustomerFormViewModel?> GetCustomerFormAsync(int id) => repository.Customers.Where(x => x.CustomerId == id)
        .Select(x => new CustomerFormViewModel { Id = x.CustomerId, CustomerName = x.CustomerName, Address = x.Address, Phone = x.Phone }).SingleOrDefaultAsync();

    public Task<int> CreateCustomerAsync(CustomerFormViewModel model, int actorId)
    {
        Validate(model);
        return repository.AddCustomerAsync(new Customer
        { CustomerName = model.CustomerName.Trim(), Address = Clean(model.Address), Phone = Clean(model.Phone) }, actorId);
    }

    public async Task<bool> UpdateCustomerAsync(int id, CustomerFormViewModel model, int actorId)
    {
        Validate(model);
        var customer = await repository.FindCustomerAsync(id);
        if (customer is null) return false;
        customer.CustomerName = model.CustomerName.Trim();
        customer.Address = Clean(model.Address);
        customer.Phone = Clean(model.Phone);
        await repository.SaveCustomerAsync(actorId);
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Validate(CustomerFormViewModel model)
    {
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(model,
            new System.ComponentModel.DataAnnotations.ValidationContext(model), validateAllProperties: true);
    }
}
