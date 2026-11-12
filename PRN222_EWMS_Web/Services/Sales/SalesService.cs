using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Sales;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.ViewModels.Sales;
using PRN222_EWMS_Web.Services.Warehouse;

namespace PRN222_EWMS_Web.Services.Sales;

public class SalesService(ISalesRepository repository, IUserRepository users,
    SalesOrderSourceService sourceOrders, ISourceReservationService sourceReservations) : ISalesService
{
    private static Expression<Func<SalesOrder, SalesOrderRowViewModel>> Row(int userId, bool canEdit = true) => o => new SalesOrderRowViewModel
    {
        Id = o.SalesOrderId, Code = o.Socode,
        CustomerName = o.CustomerNameSnapshot ?? o.Customer.CustomerName,
        WarehouseName = o.Warehouse.WarehouseName, CreatedByName = o.CreatedByUser.FullName,
        CreatedByUserId = o.CreatedByUserId, OrderDate = o.OrderDate.ToDateTime(TimeOnly.MinValue),
        Status = o.Status, HasShortage = o.HasShortage,
        CanEdit = canEdit && o.CreatedByUserId == userId && o.Status == "DRAFT" && !o.Outbounds.Any()
    };

    private sealed record OrderReadScope(bool IsSales, bool IsWarehouseStaff, int? WarehouseId, bool Allowed);

    private async Task<OrderReadScope> GetOrderReadScopeAsync(int userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user?.Status != "ACTIVE") return new(false, false, null, false);
        return user.Role?.RoleName switch
        {
            "SALES_STAFF" => new(true, false, null, true),
            "MANAGER" => new(false, false, null, true),
            "WAREHOUSE_STAFF" => new(false, true, user.WarehouseId, user.WarehouseId.HasValue),
            _ => new(false, false, null, false)
        };
    }

    private IQueryable<SalesOrder> ApplyOrderReadScope(OrderReadScope scope)
    {
        var query = repository.Orders;
        if (!scope.Allowed) return query.Where(_ => false);
        if (scope.IsWarehouseStaff)
            query = query.Where(o => o.WarehouseId == scope.WarehouseId && o.Status != "DRAFT" && o.SubmittedAt != null);
        return query;
    }

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
            RecentOrders = await repository.Orders.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.SalesOrderId).Take(5).Select(Row(userId)).ToListAsync()
        };
    }

    public async Task<SalesOrderListViewModel> GetOrdersAsync(SalesOrderListViewModel filter, int userId)
    {
        var scope = await GetOrderReadScopeAsync(userId);
        var query = ApplyOrderReadScope(scope);
        filter.CanFilterMine = scope.IsSales;
        filter.IsWarehouseStaff = scope.IsWarehouseStaff;
        filter.ScopeMessage = scope.IsWarehouseStaff && !scope.WarehouseId.HasValue
            ? "Bạn chưa được phân công kho làm việc. Vui lòng liên hệ quản lý để được phân công kho."
            : null;
        if (!scope.IsSales) filter.Mine = false;
        if (scope.IsWarehouseStaff) filter.WarehouseId = scope.WarehouseId;
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
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(Row(userId, scope.IsSales)).ToListAsync();
        var warehouses = repository.Warehouses;
        if (!scope.Allowed) warehouses = warehouses.Where(_ => false);
        else if (scope.IsWarehouseStaff) warehouses = warehouses.Where(w => w.WarehouseId == scope.WarehouseId);
        filter.Warehouses = await warehouses.OrderBy(x => x.WarehouseName)
            .Select(x => new SelectListItem(x.WarehouseName, x.WarehouseId.ToString())).ToListAsync();
        return filter;
    }

    public async Task<SalesOrderDetailsViewModel?> GetOrderAsync(long id, int userId)
    {
        var scope = await GetOrderReadScopeAsync(userId);
        var model = await ApplyOrderReadScope(scope)
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
            CanEdit = scope.IsSales && o.CreatedByUserId == userId && o.Status == "DRAFT" && !o.Outbounds.Any(),
            CanCancel = (scope.IsSales && o.CreatedByUserId == userId || !scope.IsSales && !scope.IsWarehouseStaff && scope.Allowed)
                && (o.Status == "DRAFT" || o.Status == "SUBMITTED") && !o.Outbounds.Any(),
            CanReplaceSources = scope.IsSales && o.CreatedByUserId == userId && (o.Status == "SUBMITTED" || o.Status == "PARTIALLY_FULFILLED")
                && o.SalesOrderLines.Any(l => l.SourcePlans.Any()),
            Lines = o.SalesOrderLines.OrderBy(l => l.SolineId).Select(l => new SalesOrderLineViewModel
            {
                Id = l.SolineId, ProductCode = l.Product.ProductCode, ProductName = l.Product.ProductName,
                RequestedQuantity = l.RequestedQuantity, CancelledQuantity = l.CancelledQuantity,
                IssuedQuantity = l.OutboundItemSolines.Count(i => i.IsActive && i.Outbound.Status == "DONE")
            }).ToList(),
            Outbounds = o.Outbounds.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.OutboundId).Select(x => new OutboundSummaryViewModel
            {
                Id = x.OutboundId, Code = x.IssueCode, Status = x.Status, IssueDate = x.IssueDate.ToDateTime(TimeOnly.MinValue),
                CompletedAt = x.CompletedAt, Quantity = x.OutboundItemOutbounds.Count(i => i.IsActive)
            }).ToList()
        }).SingleOrDefaultAsync();
        if (model != null)
        {
            var summaries = await sourceOrders.GetSummaryAsync(id);
            foreach (var line in model.Lines) line.Sources = summaries.GetValueOrDefault(line.Id) ?? [];
            model.CanReplaceSources &= model.Lines.Any(l => l.RemainingQuantity > l.Sources.Sum(s => s.HeldQuantity));
        }
        return model;
    }

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
        { CustomerName = model.CustomerName.Trim(), Nickname = Clean(model.Nickname), Address = Clean(model.Address), Phone = Clean(model.Phone) }, actorId);
    }

    public async Task<(bool Success, Customer? Customer, string? ErrorMessage)> QuickCreateCustomerAsync(CustomerFormViewModel model, int actorId)
    {
        var name = Clean(model.CustomerName);
        if (string.IsNullOrWhiteSpace(name))
            return (false, null, "Vui lòng nhập tên khách hàng.");

        var phone = Clean(model.Phone);
        if (string.IsNullOrWhiteSpace(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0[0-9]{9}$"))
            return (false, null, "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");

        var address = Clean(model.Address);
        if (string.IsNullOrWhiteSpace(address))
            return (false, null, "Vui lòng nhập địa chỉ khách hàng.");

        // Check trùng số điện thoại
        var isPhoneDuplicate = await repository.Customers
            .AnyAsync(c => c.Phone != null && c.Phone.Trim() == phone);
        if (isPhoneDuplicate)
            return (false, null, $"Số điện thoại {phone} đã được sử dụng bởi một khách hàng khác. Vui lòng kiểm tra lại.");

        var customer = new Customer
        {
            CustomerName = name,
            Nickname = Clean(model.Nickname),
            Phone = phone,
            Address = address
        };

        var id = await repository.AddCustomerAsync(customer, actorId);
        customer.CustomerId = id;
        return (true, customer, null);
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

    public async Task<SalesOrderFormViewModel> GetCreateFormAsync(int userId)
    {
        var code = await repository.GenerateNextSoCodeAsync();
        var warehouses = await repository.Warehouses.Where(w => w.Status == "ACTIVE").OrderBy(w => w.WarehouseName)
            .Select(w => new SelectListItem(w.WarehouseName, w.WarehouseId.ToString())).ToListAsync();
        var customers = await repository.Customers.OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem(c.CustomerName + (string.IsNullOrEmpty(c.Phone) ? "" : $" ({c.Phone})"), c.CustomerId.ToString())).ToListAsync();

        var defaultWarehouseId = warehouses.Count > 0 ? int.Parse(warehouses[0].Value) : 0;
        var availableProducts = defaultWarehouseId > 0 ? await GetAvailableProductsAsync(defaultWarehouseId) : [];

        return new SalesOrderFormViewModel
        {
            Code = code,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            WarehouseId = defaultWarehouseId > 0 ? defaultWarehouseId : null,
            Warehouses = warehouses,
            Customers = customers,
            AvailableProducts = availableProducts,
            Lines = []
        };
    }

    public async Task<SalesOrderFormViewModel?> GetEditFormAsync(long id, int userId)
    {
        var order = await repository.FindOrderForEditAsync(id);
        if (order is null) return null;

        var canEdit = order.CreatedByUserId == userId && order.Status == "DRAFT" && !order.Outbounds.Any();
        var warehouses = await repository.Warehouses.Where(w => w.Status == "ACTIVE").OrderBy(w => w.WarehouseName)
            .Select(w => new SelectListItem(w.WarehouseName, w.WarehouseId.ToString())).ToListAsync();
        var customers = await repository.Customers.OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem(c.CustomerName + (string.IsNullOrEmpty(c.Phone) ? "" : $" ({c.Phone})"), c.CustomerId.ToString())).ToListAsync();
        var availableProducts = await GetAvailableProductsAsync(order.WarehouseId, order.RequestedDate);

        var productLookup = availableProducts.ToDictionary(p => p.ProductId);

        var lines = order.SalesOrderLines.OrderBy(l => l.SolineId).Select(l => new SalesOrderLineFormDto
        {
            SolineId = l.SolineId,
            ProductId = l.ProductId,
            ProductCode = l.Product.ProductCode,
            ProductName = l.Product.ProductName,
            Unit = l.Product.Unit,
            RequestedQuantity = l.RequestedQuantity,
            Sources = l.SourcePlans.Select(p => new SalesSourceChoiceInput { SupplySourceId = p.SupplySourceId, Quantity = p.Quantity }).ToList(),
            AvailableQuantity = productLookup.TryGetValue(l.ProductId, out var p) ? p.UnreservedQuantity : 0
        }).ToList();

        return new SalesOrderFormViewModel
        {
            Id = order.SalesOrderId,
            Code = order.Socode,
            CustomerId = order.CustomerId,
            WarehouseId = order.WarehouseId,
            OrderDate = order.OrderDate,
            RequestedDate = order.RequestedDate,
            Note = order.Note,
            Status = order.Status,
            CanEdit = canEdit,
            CanCancel = order.CreatedByUserId == userId && (order.Status is "DRAFT" or "SUBMITTED") && !order.Outbounds.Any(),
            ConcurrencyToken = Convert.ToBase64String(order.RowVersion),
            Warehouses = warehouses,
            Customers = customers,
            AvailableProducts = availableProducts,
            Lines = lines
        };
    }

    public Task<long> CreateOrderAsync(SalesOrderFormViewModel model, string action, int actorId) =>
        sourceOrders.SaveAsync(null, model, action, actorId);

    public async Task<bool> UpdateOrderAsync(long id, SalesOrderFormViewModel model, string action, int actorId)
    {
        await sourceOrders.SaveAsync(id, model, action, actorId);
        return true;
    }

    public Task<(bool Success, string Message)> CancelOrderAsync(long id, int actorId, string actorRole, string? cancelReason = null) =>
        sourceOrders.CancelAsync(id, actorId, actorRole, cancelReason);

    public Task<List<SalesSourceOption>> GetSourceOptionsAsync(int warehouseId, DateOnly? requestedDate) =>
        sourceReservations.GetOptionsAsync(warehouseId, requestedDate);

    public Task<SalesOrderFormViewModel?> GetReplacementFormAsync(long id, int userId) => sourceOrders.GetReplacementFormAsync(id, userId);
    public Task AddReplacementAsync(long id, SalesOrderFormViewModel form, int actorId) => sourceOrders.AddReplacementAsync(id, form, actorId);

    public async Task<List<ProductStockLookupDto>> GetAvailableProductsAsync(int warehouseId, DateOnly? requestedDate = null)
    {
        var sources = await sourceReservations.GetOptionsAsync(warehouseId, requestedDate);
        var quantities = sources.Where(s => s.CanSelect).GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.AvailableQuantity));
        var products = await repository.Products.Where(p => p.Status == "ACTIVE").OrderBy(p => p.ProductCode)
            .Select(p => new ProductStockLookupDto { ProductId = p.ProductId, ProductCode = p.ProductCode,
                ProductName = p.ProductName, Unit = p.Unit ?? "", WarehouseId = warehouseId })
            .ToListAsync();
        foreach (var p in products) p.UnreservedQuantity = quantities.GetValueOrDefault(p.ProductId);
        return products;
    }
}
