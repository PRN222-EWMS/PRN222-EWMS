using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PRN222_EWMS_Web.ViewModels.Transfer;

public class TransferListViewModel
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    public List<TransferListItemDto> Items { get; set; } = [];
    public List<LookupItemDto> Warehouses { get; set; } = [];
    public bool CanCreate { get; set; }
}

public class TransferListItemDto
{
    public long TransferOrderId { get; set; }
    public string TransferCode { get; set; } = null!;
    public int FromWarehouseId { get; set; }
    public string FromWarehouseName { get; set; } = null!;
    public int ToWarehouseId { get; set; }
    public string ToWarehouseName { get; set; } = null!;
    public string ItemCondition { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly? DispatchDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = null!;
    public string? AssignedToName { get; set; }
    public int TotalQuantity { get; set; }
    public bool CanEdit { get; set; }
}

public class TransferDetailsViewModel
{
    public long TransferOrderId { get; set; }
    public string TransferCode { get; set; } = null!;
    public int FromWarehouseId { get; set; }
    public string FromWarehouseName { get; set; } = null!;
    public int ToWarehouseId { get; set; }
    public string ToWarehouseName { get; set; } = null!;
    public string ItemCondition { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly? DispatchDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = null!;
    public string? AssignedToName { get; set; }
    public string? Note { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool CanEdit { get; set; }

    public List<TransferLineItemDto> Lines { get; set; } = [];
    public List<TransferDispatchSummaryDto> Dispatches { get; set; } = [];
}

public class TransferLineItemDto
{
    public long TransferLineId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string? Model { get; set; }
    public string Unit { get; set; } = null!;
    public int RequestedQuantity { get; set; }
    public int CancelledQuantity { get; set; }
    public int DispatchedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RemainingToDispatch => Math.Max(0, RequestedQuantity - CancelledQuantity - DispatchedQuantity);
}

public class TransferDispatchSummaryDto
{
    public long DispatchId { get; set; }
    public string DispatchCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
}

public class CreateTransferOrderViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn kho nguồn.")]
    public int FromWarehouseId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn kho đích.")]
    public int ToWarehouseId { get; set; }
    [RegularExpression("^(GOOD|DAMAGED)$", ErrorMessage = "Tình trạng hàng không hợp lệ.")]
    public string ItemCondition { get; set; } = "GOOD";
    public DateOnly? DispatchDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public int? AssignedToUserId { get; set; }
    [StringLength(1000, ErrorMessage = "Ghi chú tối đa 1.000 ký tự.")]
    public string? Note { get; set; }
    public bool IsRelease { get; set; }

    [MinLength(1, ErrorMessage = "Cần ít nhất một sản phẩm.")]
    [MaxLength(100, ErrorMessage = "Mỗi lệnh tối đa 100 dòng sản phẩm.")]
    public List<CreateTransferOrderLineInput> Lines { get; set; } = [];

    [BindNever]
    public List<LookupItemDto> Warehouses { get; set; } = [];
    [BindNever]
    public List<ProductLookupDto> Products { get; set; } = [];
    [BindNever]
    public List<LookupItemDto> StaffUsers { get; set; } = [];
    [BindNever]
    public TransferAssessment Assessment { get; set; } = new();
    [BindNever]
    public bool CanEdit { get; set; } = true;
    [BindNever]
    public string? LockReason { get; set; }
}

public class CreateTransferOrderLineInput
{
    public long TransferLineId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn sản phẩm.")]
    public int ProductId { get; set; }
    [Range(1, 1000000, ErrorMessage = "Số lượng phải là số nguyên từ 1 đến 1.000.000.")]
    public int Quantity { get; set; }
    [BindNever]
    public long AvailableQuantity { get; set; }
}

public class EditTransferOrderViewModel : CreateTransferOrderViewModel
{
    public long TransferOrderId { get; set; }
    [BindNever]
    public string TransferCode { get; set; } = string.Empty;
    [BindNever]
    public string Status { get; set; } = string.Empty;
    [BindNever]
    public byte[] RowVersion { get; set; } = [];
    [Required(ErrorMessage = "Thiếu phiên bản dữ liệu. Hãy tải lại trang.")]
    public string RowVersionBase64 { get; set; } = string.Empty;
    [BindNever]
    public bool CanCancel { get; set; }
}

public sealed class TransferAssessment
{
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string? CurrentCapacity { get; set; }
    public string? ForecastCapacity { get; set; }
    public bool CanRelease => Errors.Count == 0;
}

public class ProductLookupDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public long UnreservedQuantity { get; set; }
}

public class LookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
