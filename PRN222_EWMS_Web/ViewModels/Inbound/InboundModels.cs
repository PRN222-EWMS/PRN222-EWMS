using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Inbound;

public class InboundListViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<InboundSummary> Items { get; set; } = [];
}

public record InboundSummary(long Id, string Code, string PoCode, string Supplier, string Warehouse,
    string Status, int Received, int Ordered, string CreatedBy, DateTime CreatedAt, DateTime? CompletedAt);

public class InboundCreateViewModel
{
    [Required] public long PurchaseOrderId { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
    [Required] public string RequestToken { get; set; } = Guid.NewGuid().ToString("N");
    public List<InboundOrderOption> Orders { get; set; } = [];
}

public record InboundOrderOption(long Id, string Code, string Supplier, string Warehouse, int Remaining);

public class InboundDetailViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "";
    public string PoCode { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? PerformedBy { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
    public string Version { get; set; } = "";
    public List<InboundLineProgress> Lines { get; set; } = [];
    public List<InboundSerialRow> Serials { get; set; } = [];
    public ReceiveSerialInput NewSerial { get; set; } = new();
}

public record InboundLineProgress(long Id, string ProductCode, string ProductName, int Ordered, int Closed,
    int Received, int InThisDraft, int InOtherDrafts, int Available);

public record InboundSerialRow(long Id, long LineId, string Serial, string Product, int? Month, int? Year,
    string Condition, string? Note, string? CaptureMethod, string? ScannedBy, DateTime ScannedAt, bool IsActive);

public class ReceiveSerialInput
{
    [Required] public long LineId { get; set; }
    [Required, StringLength(100)] public string Serial { get; set; } = "";
    [Range(1, 12)] public int Month { get; set; }
    [Range(1900, 9999)] public int Year { get; set; }
    [Required] public string Condition { get; set; } = "GOOD";
    [StringLength(1000)] public string? ConditionNote { get; set; }
    [Required] public string CaptureMethod { get; set; } = "MANUAL";
    [Required] public string Version { get; set; } = "";
}

public class InboundPendingPutawayViewModel
{
    public List<PendingPutawayRow> Items { get; set; } = [];
}

public record PendingPutawayRow(long InboundId, string InboundCode, string Serial, string ProductCode,
    string ProductName, string Condition, string Warehouse, DateTime ReceivedAt);
