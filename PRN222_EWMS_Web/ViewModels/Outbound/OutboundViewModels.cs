namespace PRN222_EWMS_Web.ViewModels.Outbound;

public class OutboundListViewModel
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    public List<OutboundListItemDto> Items { get; set; } = [];
    public List<LookupItemDto> Warehouses { get; set; } = [];
    public bool CanCreate { get; set; }
}

public class OutboundListItemDto
{
    public long OutboundId { get; set; }
    public string IssueCode { get; set; } = null!;
    public long SalesOrderId { get; set; }
    public string Socode { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActivePicking { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedByName { get; set; } = null!;
    public string? AssignedToName { get; set; }
    public int ItemCount { get; set; }
}

public class OutboundDetailsViewModel
{
    // Thông tin phiếu xuất
    public long OutboundId { get; set; }
    public string IssueCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActivePicking { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RecipientName { get; set; }
    public string? Note { get; set; }

    // Kho xuất
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string? WarehouseAddress { get; set; }

    // Nhân sự
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = null!;
    public int? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }

    // Thông tin khách hàng (Snapshot lưu trên phiếu xuất)
    public string CustomerName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }

    // Đơn bán hàng liên quan (Sales Order)
    public long SalesOrderId { get; set; }
    public string Socode { get; set; } = null!;
    public string SoStatus { get; set; } = null!;
    public DateOnly SoOrderDate { get; set; }
    public DateOnly? SoRequestedDate { get; set; }
    public bool SoHasShortage { get; set; }

    // Tiến độ fulfillment từng dòng sản phẩm của Sales Order
    public List<OutboundLineProgressDto> Lines { get; set; } = [];

    // Danh sách serial thuộc phiếu xuất
    public List<OutboundSerialItemDto> Serials { get; set; } = [];

    // Thống kê tổng hợp
    public int TotalRequested => Lines.Sum(l => l.RequestedQuantity);
    public long TotalDone => Lines.Sum(l => l.DoneQuantity);
    public long TotalRemaining => Lines.Sum(l => l.RemainingQuantity);
    public int TotalInThisReceipt => Serials.Count(s => s.IsActive);
    public int TotalDoneInThisReceipt => Serials.Count(s => s.IsActive && s.IsDone);
    public int TotalPickingInThisReceipt => Serials.Count(s => s.IsActive && !s.IsDone);

    public int FulfillmentPercent => TotalRequested > 0
        ? (int)Math.Min(100, Math.Round((double)TotalDone / TotalRequested * 100))
        : 0;
}

public class OutboundLineProgressDto
{
    public long SolineId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string? Model { get; set; }
    public string Unit { get; set; } = null!;
    public int RequestedQuantity { get; set; }
    public int CancelledQuantity { get; set; }
    public long DoneQuantity { get; set; }
    public long RemainingQuantity { get; set; }
    public long ReservedBalance { get; set; }
    public int InThisReceiptCount { get; set; }

    public int LineProgressPercent => RequestedQuantity > 0
        ? (int)Math.Min(100, Math.Round((double)DoneQuantity / RequestedQuantity * 100))
        : 0;
}

public class OutboundSerialItemDto
{
    public long OutboundItemId { get; set; }
    public long SerialItemId { get; set; }
    public string SerialNumber { get; set; } = null!;
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string ItemCondition { get; set; } = null!;
    public DateTime ScannedAt { get; set; }
    public bool IsActive { get; set; }

    // Phân biệt trạng thái: Đã Done hay đang được Claim/Picking
    public bool IsDone { get; set; }
    public bool IsClaimed { get; set; }
    public string? ClaimedByName { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public string StatusBadgeText => IsDone ? "Đã xuất kho" : IsClaimed ? "Đang lấy hàng" : "Trong phiếu nháp";
    public string StatusBadgeClass => IsDone ? "outbound-serial-done" : IsClaimed ? "outbound-serial-picking" : "outbound-serial-draft";
}

public class LookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

// =============================================================================
// CHỨC NĂNG 70: CREATE OUTBOUND RECEIPT & PICKING WORKBENCH
// =============================================================================

public class CreateOutboundViewModel
{
    public long? SelectedSalesOrderId { get; set; }
    public List<EligibleSalesOrderDto> EligibleOrders { get; set; } = [];
}

public class EligibleSalesOrderDto
{
    public long SalesOrderId { get; set; }
    public string Socode { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? ShippingAddress { get; set; }
    public DateOnly? RequestedDate { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int TotalRequested { get; set; }
    public long TotalDone { get; set; }
    public long TotalRemaining { get; set; }
    public long TotalReserved { get; set; }
    public bool HasShortage { get; set; }
    public List<OutboundLineProgressDto> Lines { get; set; } = [];
}

public class PickingWorkbenchViewModel
{
    public long OutboundId { get; set; }
    public string IssueCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActivePicking { get; set; }
    public DateTime? StartedAt { get; set; }

    public long SalesOrderId { get; set; }
    public string Socode { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }

    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;

    public int AssignedToUserId { get; set; }
    public string AssignedToName { get; set; } = null!;

    public List<OutboundLineProgressDto> Lines { get; set; } = [];
    public List<OutboundSerialItemDto> ScannedSerials { get; set; } = [];

    public int TotalScanned => ScannedSerials.Count(s => s.IsActive);
    public long TotalRemainingToPick => Lines.Sum(l => l.RemainingQuantity) - TotalScanned;

    public string? SerialInput { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

