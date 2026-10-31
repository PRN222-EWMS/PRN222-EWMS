using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Purchasing;

// ==========================================
// 1. VIEWMODEL CHO DANH SÁCH (INDEX)
// ==========================================
public class PoListViewModel
{
    // Các trường lọc (Filter)
    public string? SearchTerm { get; set; }
    public int? WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public string? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool OnlyMyOrders { get; set; } = false;

    // Phân trang
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    // Dữ liệu danh sách
    public List<PoListItemDto> Items { get; set; } = [];

    // Dropdown dữ liệu phục vụ bộ lọc
    public List<LookupItemDto> Warehouses { get; set; } = [];
    public List<LookupItemDto> Suppliers { get; set; } = [];

    // Quyền của người xem hiện tại
    public bool CanCreateNew { get; set; }
}

public class PoListItemDto
{
    public long PurchaseOrderId { get; set; }
    public string Pocode { get; set; } = null!;
    public string SupplierName { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public string CreatedByName { get; set; } = null!;
    public int CreatedByUserId { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly ExpectedArrivalDate { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ConfirmedAt { get; set; }

    // Chỉ số tổng hợp tiến độ
    public int TotalOrderedQuantity { get; set; }
    public long TotalReceivedQuantity { get; set; }
    public long TotalRemainingQuantity { get; set; }
    public int TotalClosedQuantity { get; set; }
    
    // Tỷ lệ hoàn thành %
    public int ProgressPercent => (TotalOrderedQuantity - TotalClosedQuantity) > 0 
        ? (int)Math.Min(100, Math.Round((double)TotalReceivedQuantity / (TotalOrderedQuantity - TotalClosedQuantity) * 100))
        : 0;

    // Cờ trạng thái đặc biệt
    public bool IsOverdue => ExpectedArrivalDate < DateOnly.FromDateTime(DateTime.Today) 
                             && Status != "RECEIVED" && Status != "CLOSED" && Status != "CANCELLED";

    public bool CanEdit { get; set; }
}

// ==========================================
// 2. VIEWMODEL CHO TRANG CHI TIẾT (DETAILS)
// ==========================================
public class PoDetailsViewModel
{
    public long PurchaseOrderId { get; set; }
    public string Pocode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly OrderDate { get; set; }
    public DateOnly ExpectedArrivalDate { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Thông tin Đối tác & Kho
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? SupplierPhone { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierAddress { get; set; }

    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string WarehouseType { get; set; } = null!;
    public string? WarehouseAddress { get; set; }

    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = null!;

    // Danh sách dòng sản phẩm (Product Lines)
    public List<PoLineDetailDto> Lines { get; set; } = [];

    // Danh sách các đợt nhận hàng liên quan (Inbounds)
    public List<PoInboundReceiptDto> Inbounds { get; set; } = [];

    // Tổng số lượng đơn
    public int TotalOrdered => Lines.Sum(l => l.OrderedQuantity);
    public long TotalReceived => Lines.Sum(l => l.ReceivedQuantity);
    public long TotalRemaining => Lines.Sum(l => l.RemainingQuantity);
    public int TotalClosed => Lines.Sum(l => l.ClosedQuantity);

    public int ProgressPercent => (TotalOrdered - TotalClosed) > 0 
        ? (int)Math.Min(100, Math.Round((double)TotalReceived / (TotalOrdered - TotalClosed) * 100))
        : 0;

    public bool IsOverdue => ExpectedArrivalDate < DateOnly.FromDateTime(DateTime.Today) 
                             && Status != "RECEIVED" && Status != "CLOSED" && Status != "CANCELLED";

    // Quyền thao tác tại trang chi tiết
    public bool CanEdit { get; set; }
    public bool CanConfirm { get; set; }
    public bool CanCloseRemainder { get; set; }
    public bool CanCancelDraft { get; set; }
}

public class PoLineDetailDto
{
    public long PolineId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string BrandName { get; set; } = null!;
    public decimal GrossWeightKg { get; set; }
    public string Dimensions => $"{LengthCm:G29} x {WidthCm:G29} x {HeightCm:G29} cm";
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }

    public int OrderedQuantity { get; set; }
    public long ReceivedQuantity { get; set; }
    public long RemainingQuantity { get; set; }
    public int ClosedQuantity { get; set; }

    public string LineStatus => RemainingQuantity == 0 ? "Đã nhận đủ"
        : ReceivedQuantity > 0 ? "Đang nhận một phần"
        : ClosedQuantity > 0 ? "Đã đóng"
        : "Chờ nhận hàng";
}

public class PoInboundReceiptDto
{
    public long InboundId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public DateOnly ReceiptDate { get; set; }
    public string Status { get; set; } = null!;
    public string PerformedByName { get; set; } = null!;
    public DateTime? CompletedAt { get; set; }
    public int ItemCount { get; set; }
    public string? Note { get; set; }
}

// ==========================================
// 3. VIEWMODEL CHO FORM TẠO & SỬA (CREATE / EDIT)
// ==========================================
public class PoFormViewModel
{
    public long? PurchaseOrderId { get; set; }
    public string? Pocode { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn Nhà cung cấp.")]
    [Display(Name = "Nhà cung cấp")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn Kho nhận hàng.")]
    [Display(Name = "Kho tiếp nhận")]
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập Ngày dự kiến giao hàng.")]
    [Display(Name = "Ngày dự kiến đến")]
    public DateOnly ExpectedArrivalDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    [MaxLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    [Display(Name = "Ghi chú đơn mua")]
    public string? Note { get; set; }

    public string? Status { get; set; }
    public byte[]? RowVersion { get; set; }

    // Danh sách dòng sản phẩm (Bắt buộc tối thiểu 1 dòng)
    public List<PoFormLineDto> Lines { get; set; } = [];

    // Dropdown dữ liệu chọn
    public List<LookupItemDto> Warehouses { get; set; } = [];
    public List<LookupItemDto> Suppliers { get; set; } = [];
    public List<ProductLookupDto> AvailableProducts { get; set; } = [];

    // Cờ trạng thái form
    public bool IsEditMode => PurchaseOrderId.HasValue && PurchaseOrderId > 0;
    public bool IsConfirmedOrder => Status == "CONFIRMED";
}

public class PoFormLineDto
{
    public long? PolineId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
    public int ProductId { get; set; }

    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; }

    [Required(ErrorMessage = "Số lượng đặt là bắt buộc.")]
    [Range(1, 1000000, ErrorMessage = "Số lượng đặt phải là số nguyên dương lớn hơn 0.")]
    public int OrderedQuantity { get; set; } = 1;
}

// ==========================================
// 4. CÁC DTO HỖ TRỢ CHUNG
// ==========================================
public class LookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class ProductLookupDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int SupplierId { get; set; }
}

public class CloseRemainderRequestDto
{
    public long PurchaseOrderId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do đóng đơn.")]
    [MaxLength(500, ErrorMessage = "Lý do đóng không được vượt quá 500 ký tự.")]
    public string Reason { get; set; } = null!;
}
