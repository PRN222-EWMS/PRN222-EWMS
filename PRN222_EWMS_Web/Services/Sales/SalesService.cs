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

    public async Task<SalesOrderFormViewModel> GetCreateFormAsync(int userId)
    {
        var code = await repository.GenerateNextSoCodeAsync();
        var warehouses = await repository.Warehouses.OrderBy(w => w.WarehouseName)
            .Select(w => new SelectListItem(w.WarehouseName, w.WarehouseId.ToString())).ToListAsync();
        var customers = await repository.Customers.OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem(c.CustomerName + (string.IsNullOrEmpty(c.Phone) ? "" : $" ({c.Phone})"), c.CustomerId.ToString())).ToListAsync();

        var defaultWarehouseId = warehouses.Count > 0 ? int.Parse(warehouses[0].Value) : 0;
        var availableProducts = defaultWarehouseId > 0 ? await repository.GetAvailableProductsAsync(defaultWarehouseId) : [];

        return new SalesOrderFormViewModel
        {
            Code = code,
            OrderDate = DateOnly.FromDateTime(DateTime.Today),
            RequestedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
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

        var canEdit = order.CreatedByUserId == userId && (order.Status == "DRAFT" || order.Status == "SUBMITTED") && !order.Outbounds.Any();
        var warehouses = await repository.Warehouses.OrderBy(w => w.WarehouseName)
            .Select(w => new SelectListItem(w.WarehouseName, w.WarehouseId.ToString())).ToListAsync();
        var customers = await repository.Customers.OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem(c.CustomerName + (string.IsNullOrEmpty(c.Phone) ? "" : $" ({c.Phone})"), c.CustomerId.ToString())).ToListAsync();
        var availableProducts = await repository.GetAvailableProductsAsync(order.WarehouseId);

        var productLookup = availableProducts.ToDictionary(p => p.ProductId);

        var lines = order.SalesOrderLines.OrderBy(l => l.SolineId).Select(l => new SalesOrderLineFormDto
        {
            SolineId = l.SolineId,
            ProductId = l.ProductId,
            ProductCode = l.Product.ProductCode,
            ProductName = l.Product.ProductName,
            Unit = l.Product.Unit,
            RequestedQuantity = l.RequestedQuantity,
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
            ConcurrencyToken = Convert.ToBase64String(order.RowVersion),
            Warehouses = warehouses,
            Customers = customers,
            AvailableProducts = availableProducts,
            Lines = lines
        };
    }

    public async Task<long> CreateOrderAsync(SalesOrderFormViewModel model, string action, int actorId)
    {
        var isSubmit = string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase);

        var customer = await repository.FindCustomerAsync(model.CustomerId ?? 0)
            ?? throw new InvalidOperationException("Vui lòng chọn khách hàng hợp lệ.");
        var warehouse = await repository.Warehouses.SingleOrDefaultAsync(w => w.WarehouseId == (model.WarehouseId ?? 0))
            ?? throw new InvalidOperationException("Vui lòng chọn kho xuất hợp lệ.");

        if (model.Lines == null || model.Lines.Count == 0)
            throw new InvalidOperationException("Đơn bán hàng phải có ít nhất một dòng sản phẩm.");

        if (model.Lines.Any(l => !l.ProductId.HasValue || l.ProductId <= 0 || !l.RequestedQuantity.HasValue || l.RequestedQuantity <= 0))
            throw new InvalidOperationException("Tất cả sản phẩm phải được chọn và có số lượng yêu cầu lớn hơn 0.");

        if (model.Lines.Select(l => l.ProductId!.Value).Distinct().Count() != model.Lines.Count)
            throw new InvalidOperationException("Không được chọn trùng sản phẩm trên nhiều dòng.");

        if (isSubmit)
        {
            if (!model.RequestedDate.HasValue)
                throw new InvalidOperationException("Vui lòng nhập ngày yêu cầu xuất hàng khi gửi đơn.");
            if (model.RequestedDate.Value < model.OrderDate)
                throw new InvalidOperationException("Ngày yêu cầu xuất hàng phải bằng hoặc sau ngày đặt đơn.");
        }

        var code = await repository.GenerateNextSoCodeAsync();

        var order = new SalesOrder
        {
            Socode = code,
            CustomerId = customer.CustomerId,
            WarehouseId = warehouse.WarehouseId,
            CreatedByUserId = actorId,
            OrderDate = model.OrderDate,
            RequestedDate = model.RequestedDate,
            Note = Clean(model.Note),
            Status = isSubmit ? "SUBMITTED" : "DRAFT",
            SubmittedAt = isSubmit ? DateTime.UtcNow : null,
            CustomerNameSnapshot = customer.CustomerName,
            CustomerAddressSnapshot = customer.Address,
            CustomerPhoneSnapshot = customer.Phone,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var lineDto in model.Lines)
        {
            order.SalesOrderLines.Add(new SalesOrderLine
            {
                ProductId = lineDto.ProductId!.Value,
                RequestedQuantity = lineDto.RequestedQuantity!.Value,
                CancelledQuantity = 0
            });
        }

        await repository.AddOrderAsync(order);

        order.HasShortage = await AllocateReservationsAsync(order, isSubmit);

        await repository.SaveChangesWithActorAsync(actorId);
        return order.SalesOrderId;
    }

    public async Task<bool> UpdateOrderAsync(long id, SalesOrderFormViewModel model, string action, int actorId)
    {
        var order = await repository.FindOrderForEditAsync(id);
        if (order is null) return false;

        if (order.CreatedByUserId != actorId)
            throw new InvalidOperationException("Chỉ người tạo đơn mới được phép chỉnh sửa đơn hàng.");

        if (order.Outbounds.Any())
            throw new InvalidOperationException("Đơn bán hàng đã phát sinh phiếu xuất kho, thông tin đơn đã bị khóa.");

        if (order.Status != "DRAFT" && order.Status != "SUBMITTED")
            throw new InvalidOperationException("Trạng thái đơn hàng không cho phép chỉnh sửa.");

        if (!string.IsNullOrEmpty(model.ConcurrencyToken) && Convert.ToBase64String(order.RowVersion) != model.ConcurrencyToken)
            throw new DbUpdateConcurrencyException("Dữ liệu đơn hàng đã thay đổi bởi thao tác khác. Vui lòng tải lại trang.");

        var isSubmit = string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase) || order.Status == "SUBMITTED";

        var customer = await repository.FindCustomerAsync(model.CustomerId ?? 0)
            ?? throw new InvalidOperationException("Vui lòng chọn khách hàng hợp lệ.");
        var warehouse = await repository.Warehouses.SingleOrDefaultAsync(w => w.WarehouseId == (model.WarehouseId ?? 0))
            ?? throw new InvalidOperationException("Vui lòng chọn kho xuất hợp lệ.");

        if (model.Lines == null || model.Lines.Count == 0)
            throw new InvalidOperationException("Đơn bán hàng phải có ít nhất một dòng sản phẩm.");

        if (model.Lines.Any(l => !l.ProductId.HasValue || l.ProductId <= 0 || !l.RequestedQuantity.HasValue || l.RequestedQuantity <= 0))
            throw new InvalidOperationException("Tất cả sản phẩm phải được chọn và có số lượng yêu cầu lớn hơn 0.");

        if (model.Lines.Select(l => l.ProductId!.Value).Distinct().Count() != model.Lines.Count)
            throw new InvalidOperationException("Không được chọn trùng sản phẩm trên nhiều dòng.");

        if (isSubmit)
        {
            if (!model.RequestedDate.HasValue)
                throw new InvalidOperationException("Vui lòng nhập ngày yêu cầu xuất hàng.");
            if (model.RequestedDate.Value < model.OrderDate)
                throw new InvalidOperationException("Ngày yêu cầu xuất hàng phải bằng hoặc sau ngày đặt đơn.");
        }

        order.CustomerId = customer.CustomerId;
        order.WarehouseId = warehouse.WarehouseId;
        order.OrderDate = model.OrderDate;
        order.RequestedDate = model.RequestedDate;
        order.Note = Clean(model.Note);
        order.CustomerNameSnapshot = customer.CustomerName;
        order.CustomerAddressSnapshot = customer.Address;
        order.CustomerPhoneSnapshot = customer.Phone;

        if (order.Status == "DRAFT" && string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "SUBMITTED";
            order.SubmittedAt = DateTime.UtcNow;
        }

        // Close any active reservations prior to re-allocation directly on tracked entities
        foreach (var r in order.Reservations.Where(r => r.Status == "ACTIVE"))
        {
            r.Status = "CLOSED";
            r.ReleasedQuantity = r.ReservedQuantity - r.ConsumedQuantity;
            r.ClosedAt = DateTime.UtcNow;
        }

        // Update lines
        var incomingSolineIds = model.Lines.Where(l => l.SolineId > 0).Select(l => l.SolineId).ToHashSet();
        var linesToRemove = order.SalesOrderLines.Where(l => !incomingSolineIds.Contains(l.SolineId)).ToList();
        await repository.RemoveOrderLinesAsync(linesToRemove);

        foreach (var lineDto in model.Lines.Where(l => l.SolineId > 0))
        {
            var line = order.SalesOrderLines.Single(l => l.SolineId == lineDto.SolineId);
            line.ProductId = lineDto.ProductId!.Value;
            line.RequestedQuantity = lineDto.RequestedQuantity!.Value;
        }

        foreach (var lineDto in model.Lines.Where(l => l.SolineId == 0))
        {
            order.SalesOrderLines.Add(new SalesOrderLine
            {
                ProductId = lineDto.ProductId!.Value,
                RequestedQuantity = lineDto.RequestedQuantity!.Value,
                CancelledQuantity = 0
            });
        }

        order.HasShortage = await AllocateReservationsAsync(order, isSubmit);

        await repository.SaveChangesWithActorAsync(actorId);
        return true;
    }

    public Task<List<ProductStockLookupDto>> GetAvailableProductsAsync(int warehouseId) =>
        repository.GetAvailableProductsAsync(warehouseId);

    private async Task<bool> AllocateReservationsAsync(SalesOrder order, bool isSubmit)
    {
        var heldBySource = await repository.Reservations
            .Where(r => r.Status == "ACTIVE" && r.WarehouseId == order.WarehouseId)
            .GroupBy(r => r.SupplySourceId)
            .Select(g => new { SupplySourceId = g.Key, ReservedBalance = g.Sum(r => (long)r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity) })
            .ToDictionaryAsync(x => x.SupplySourceId, x => x.ReservedBalance);

        var rawSources = await repository.SupplySources
            .Where(s => s.WarehouseId == order.WarehouseId && s.ItemCondition == "GOOD")
            .ToListAsync();

        var sources = rawSources
            .Select(s =>
            {
                var reserved = heldBySource.TryGetValue(s.SupplySourceId, out var b) ? b : 0;
                return new
                {
                    Entity = s,
                    s.SupplySourceId,
                    s.ProductId,
                    s.WarehouseId,
                    s.SupplyState,
                    s.AvailableFrom,
                    s.EligibleQuantity,
                    Unreserved = (long)s.EligibleQuantity - reserved
                };
            })
            .Where(x => x.Unreserved > 0)
            // Áp dụng Cutoff ngày theo Mục 11 & Test Case SO15:
            // Nguồn incoming chỉ được dùng nếu ngày hàng về AvailableFrom <= RequestedDate của khách
            .Where(x => x.SupplyState == "PHYSICAL" 
                     || !order.RequestedDate.HasValue 
                     || x.AvailableFrom == null 
                     || x.AvailableFrom <= order.RequestedDate.Value)
            .ToList();

        var orderedSources = sources
            .OrderBy(s => s.SupplyState == "PHYSICAL" ? 0 : 1)
            .ThenBy(s => s.AvailableFrom)
            .ThenBy(s => s.SupplySourceId)
            .ToList();

        var sourcePool = orderedSources.ToDictionary(s => s.SupplySourceId, s => s.Unreserved);
        var hasShortage = false;

        foreach (var line in order.SalesOrderLines)
        {
            var needed = (long)line.RequestedQuantity;
            var candidateSources = orderedSources.Where(s => s.ProductId == line.ProductId && sourcePool[s.SupplySourceId] > 0);

            foreach (var src in candidateSources)
            {
                var available = sourcePool[src.SupplySourceId];
                var take = Math.Min(needed, available);
                sourcePool[src.SupplySourceId] -= take;
                needed -= take;

                if (isSubmit)
                {
                    var reservation = new Reservation
                    {
                        SupplySource = src.Entity,
                        SupplySourceNavigation = src.Entity,
                        SupplySourceId = src.SupplySourceId,
                        Soline = line,
                        SalesOrder = order,
                        ProductId = line.ProductId,
                        WarehouseId = order.WarehouseId,
                        ItemCondition = "GOOD",
                        SupplyState = src.SupplyState,
                        ReservedQuantity = (int)take,
                        ConsumedQuantity = 0,
                        ReleasedQuantity = 0,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow
                    };
                    if (line.SolineId > 0) reservation.SolineId = line.SolineId;
                    if (order.SalesOrderId > 0) reservation.SalesOrderId = order.SalesOrderId;

                    order.Reservations.Add(reservation);
                }

                if (needed == 0) break;
            }

            if (needed > 0)
            {
                hasShortage = true;

                // Theo Phân tích SO Mục 4, Mục 7 & Test Case SO06 (MSG-037):
                // Khi Gửi đơn (Submit): Bắt buộc đủ nguồn, nếu thiếu nguồn thì từ chối Submit ngay lập tức!
                // Chỉ cho phép lưu nháp (Draft) khi chưa đủ nguồn theo Test Case SO04.
                if (isSubmit)
                {
                    var prod = await repository.Products.FirstOrDefaultAsync(p => p.ProductId == line.ProductId);
                    var prodCode = prod?.ProductCode ?? $"ID {line.ProductId}";
                    var prodName = prod?.ProductName ?? "Sản phẩm";
                    throw new InvalidOperationException($"Số lượng yêu cầu cho sản phẩm [{prodCode}] {prodName} ({line.RequestedQuantity}) vượt quá số lượng hiện có thể đặt từ kho này (còn khả dụng: {line.RequestedQuantity - needed}) theo quy định MSG-037.");
                }
            }
        }

        return hasShortage;
    }

}

