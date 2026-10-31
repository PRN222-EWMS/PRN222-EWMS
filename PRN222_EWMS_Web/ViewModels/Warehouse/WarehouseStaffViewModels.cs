namespace PRN222_EWMS_Web.ViewModels.WarehouseStaff;

public class WarehouseDashboardViewModel
{
    public int? WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "Chưa gán kho";
    public string WarehouseType { get; set; } = "";
    public string Address { get; set; } = "";
    public DateOnly? RentalStartDate { get; set; }
    public DateOnly? RentalEndDate { get; set; }
    public bool IsRental => string.Equals(WarehouseType, "RENTED", StringComparison.OrdinalIgnoreCase);
    public bool IsRentalExpiringSoon { get; set; }
    public int? RentalDaysRemaining { get; set; }
    public string? LeaseAlertMessage { get; set; }

    // Hàng đợi tác nghiệp
    public int ReceivablePoCount { get; set; }
    public int DraftInboundCount { get; set; }
    public int AwaitingPutawayCount { get; set; }
    public int PendingPickingOrderCount { get; set; }
    public int MyActivePickingCount { get; set; }
    public int TransferDispatchCount { get; set; }
    public int TransferInTransitCount { get; set; }
    public int ActiveStocktakeCount { get; set; }
    public List<string> ActiveStocktakeCodes { get; set; } = new();

    // Danh sách PO cần nhận gần nhất
    public List<WarehousePoListItemDto> RecentReceivableOrders { get; set; } = new();

    public string UserFullName { get; set; } = "";
    public string Username { get; set; } = "";
    public bool HasWarehouse => WarehouseId.HasValue && WarehouseId.Value > 0;
}

public class WarehousePoListViewModel
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public string? SearchTerm { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
    public List<WarehousePoListItemDto> Items { get; set; } = new();
}

public class WarehousePoListItemDto
{
    public long PurchaseOrderId { get; set; }
    public string Pocode { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public DateOnly? ExpectedArrivalDate { get; set; }
    public bool IsOverdue => ExpectedArrivalDate.HasValue && ExpectedArrivalDate.Value < DateOnly.FromDateTime(DateTime.Today);
    public long TotalOrdered { get; set; }
    public long TotalReceived { get; set; }
    public long TotalRemaining { get; set; }
    public string Status { get; set; } = "";
}

public class WarehousePoDetailsViewModel
{
    public long PurchaseOrderId { get; set; }
    public string Pocode { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly? ExpectedArrivalDate { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? Note { get; set; }

    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public string? SupplierPhone { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierAddress { get; set; }

    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public string WarehouseAddress { get; set; } = "";
    public string WarehouseType { get; set; } = "";

    public long TotalOrdered { get; set; }
    public long TotalReceived { get; set; }
    public long TotalRemaining { get; set; }
    public bool CanStartReceiving => TotalRemaining > 0;

    public List<WarehousePoLineDto> Lines { get; set; } = new();
    public List<WarehousePoReceiptDto> PreviousReceipts { get; set; } = new();
}

public class WarehousePoLineDto
{
    public long PolineId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? Model { get; set; }
    public string Unit { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string BrandName { get; set; } = "";
    public string? Specifications { get; set; }

    public decimal? GrossWeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }

    public long OrderedQuantity { get; set; }
    public long ReceivedQuantity { get; set; }
    public long RemainingQuantity { get; set; }
    public string LineStatus => RemainingQuantity == 0 ? "Hoàn thành" : (ReceivedQuantity > 0 ? "Đã nhận một phần" : "Chờ nhận");
}

public class WarehousePoReceiptDto
{
    public long InboundId { get; set; }
    public string ReceiptCode { get; set; } = "";
    public DateOnly ReceiptDate { get; set; }
    public string Status { get; set; } = "";
    public string PerformedByName { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
    public int ItemCount { get; set; }
    public string? Note { get; set; }
}
