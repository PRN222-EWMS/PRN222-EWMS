namespace PRN222_EWMS_Web.ViewModels.Manager;

public class ManagerInboundFilter
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
}

public class ManagerInboundListViewModel
{
    public ManagerInboundFilter Filter { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<ManagerInboundSummary> Receipts { get; set; } = [];
    public List<ManagerWarehouseOption> Warehouses { get; set; } = [];
}

public record ManagerWarehouseOption(int Id, string Name);
public record ManagerInboundSummary(long Id, string ReceiptCode, string PoCode, string Supplier,
    string Warehouse, string Status, DateOnly ReceiptDate, DateTime CreatedAt,
    int Scanned, int Accepted, long PoOrdered, long PoReceived, long PoOutstanding, long PoClosed);

public class ManagerInboundDetailViewModel
{
    public long Id { get; set; }
    public string ReceiptCode { get; set; } = "";
    public string PoCode { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly ReceiptDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? PerformedBy { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
    public int Scanned { get; set; }
    public int Accepted { get; set; }
    public long PoOrdered { get; set; }
    public long PoReceived { get; set; }
    public long PoOutstanding { get; set; }
    public long PoClosed { get; set; }
    public List<ManagerInboundLine> Lines { get; set; } = [];
    public List<ManagerInboundSerial> Serials { get; set; } = [];
}

public record ManagerInboundLine(string ProductCode, string ProductName,
    long Ordered, long Received, long Outstanding, long Closed, int InThisReceiptAccepted, int InThisReceiptScanned);
public record ManagerInboundSerial(string Serial, string ProductCode, string ProductName,
    byte? ManufacturingMonth, short? ManufacturingYear, string? Condition, string? ConditionNote,
    string? CaptureMethod, DateTime ScannedAt, bool Accepted);
