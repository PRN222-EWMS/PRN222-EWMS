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
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public string ItemCondition { get; set; } = "GOOD";
    public DateOnly? DispatchDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public int? AssignedToUserId { get; set; }
    public string? Note { get; set; }
    public bool IsRelease { get; set; }

    public List<CreateTransferOrderLineInput> Lines { get; set; } = [];

    public List<LookupItemDto> Warehouses { get; set; } = [];
    public List<ProductLookupDto> Products { get; set; } = [];
    public List<LookupItemDto> StaffUsers { get; set; } = [];
}

public class CreateTransferOrderLineInput
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public long AvailableQuantity { get; set; }
}

public class EditTransferOrderViewModel
{
    public long TransferOrderId { get; set; }
    public string TransferCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
    public string RowVersionBase64 { get; set; } = string.Empty;

    public int FromWarehouseId { get; set; }
    public string FromWarehouseName { get; set; } = string.Empty;
    public int ToWarehouseId { get; set; }
    public string ToWarehouseName { get; set; } = string.Empty;
    public string ItemCondition { get; set; } = "GOOD";

    public DateOnly? DispatchDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public int? AssignedToUserId { get; set; }
    public string? Note { get; set; }
    public bool IsRelease { get; set; }

    public bool IsSourceContextFrozen { get; set; }
    public bool CanCancel { get; set; }

    public List<EditTransferOrderLineInput> Lines { get; set; } = [];

    public List<LookupItemDto> Warehouses { get; set; } = [];
    public List<ProductLookupDto> Products { get; set; } = [];
    public List<LookupItemDto> StaffUsers { get; set; } = [];
}

public class EditTransferOrderLineInput
{
    public long TransferLineId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int MinQuantityAllowed { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public long AvailableQuantity { get; set; }
    public int DispatchedQuantity { get; set; }
    public bool IsProductFrozen => DispatchedQuantity > 0;
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
