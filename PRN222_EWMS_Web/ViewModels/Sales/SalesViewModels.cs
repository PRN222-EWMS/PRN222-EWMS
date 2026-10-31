using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Sales;

public class SalesOrderRowViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public int CreatedByUserId { get; set; }
    public string Status { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public bool HasShortage { get; set; }
    public bool CanEdit { get; set; }
}

public class SalesDashboardViewModel
{
    public int TotalOrders { get; set; }
    public int WaitingOrders { get; set; }
    public int PartialOrders { get; set; }
    public int FulfilledOrders { get; set; }
    public int ShortageOrders { get; set; }
    public List<SalesOrderRowViewModel> RecentOrders { get; set; } = [];
}

public class SalesOrderListViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool Mine { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize => 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public List<SalesOrderRowViewModel> Items { get; set; } = [];
    public List<SelectListItem> Warehouses { get; set; } = [];
}

public class SalesOrderDetailsViewModel : SalesOrderRowViewModel
{
    public DateTime? RequestedDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Note { get; set; }
    public string? CloseReason { get; set; }
    public List<SalesOrderLineViewModel> Lines { get; set; } = [];
    public List<OutboundSummaryViewModel> Outbounds { get; set; } = [];
}

public class SalesOrderLineViewModel
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int RequestedQuantity { get; set; }
    public int IssuedQuantity { get; set; }
    public int CancelledQuantity { get; set; }
    public int RemainingQuantity => RequestedQuantity - IssuedQuantity - CancelledQuantity;
}

public class OutboundSummaryViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime IssueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Quantity { get; set; }
}

public class CustomerRowViewModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
}

public class CustomerListViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize => 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public List<CustomerRowViewModel> Items { get; set; } = [];
}

public class CustomerDetailsViewModel : CustomerRowViewModel
{
    public List<SalesOrderRowViewModel> Orders { get; set; } = [];
}

public class CustomerFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên khách hàng."), StringLength(200, ErrorMessage = "Tên khách hàng tối đa 200 ký tự.")]
    [Display(Name = "Tên khách hàng")]
    public string CustomerName { get; set; } = "";
    [StringLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự."), Display(Name = "Địa chỉ")]
    public string? Address { get; set; }
    [RegularExpression(@"^0[0-9]{9}$",
        ErrorMessage = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }
}

public class SalesOrderFormViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn khách hàng.")]
    [Display(Name = "Khách hàng")]
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn kho xuất.")]
    [Display(Name = "Kho xuất hàng")]
    public int? WarehouseId { get; set; }

    [Display(Name = "Ngày đặt hàng")]
    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Ngày yêu cầu xuất hàng")]
    public DateOnly? RequestedDate { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public string Status { get; set; } = "DRAFT";
    public bool CanEdit { get; set; } = true;
    public bool IsEditMode => Id > 0;
    public string? ConcurrencyToken { get; set; }

    public List<SalesOrderLineFormDto> Lines { get; set; } = [];

    public List<SelectListItem> Customers { get; set; } = [];
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<ProductStockLookupDto> AvailableProducts { get; set; } = [];
}

public class SalesOrderLineFormDto
{
    public long SolineId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
    public int? ProductId { get; set; }

    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số lượng yêu cầu.")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng yêu cầu phải lớn hơn 0.")]
    public int? RequestedQuantity { get; set; }

    public long AvailableQuantity { get; set; }
}

public class ProductStockLookupDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "";
    public int WarehouseId { get; set; }
    public long UnreservedQuantity { get; set; }
}

