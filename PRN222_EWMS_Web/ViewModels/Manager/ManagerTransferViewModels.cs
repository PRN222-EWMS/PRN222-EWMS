namespace PRN222_EWMS_Web.ViewModels.Manager;

public record ManagerTransferSummary(
    long Id,
    string TransferCode,
    string FromWarehouse,
    string ToWarehouse,
    string Status,
    DateTime CreatedAt,
    long Requested,
    long Cancelled,
    long Dispatched,
    long Received,
    long InTransit)
{
    public long Outstanding => Requested - Cancelled - Dispatched;
}

public class ManagerTransferListViewModel
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
    public List<string> StatusOptions { get; set; } = [];
    public List<ManagerTransferSummary> Orders { get; set; } = [];
}

public class ManagerTransferDetail
{
    public string TransferCode { get; set; } = "";
    public string FromWarehouse { get; set; } = "";
    public string ToWarehouse { get; set; } = "";
    public string Status { get; set; } = "";
    public string ItemCondition { get; set; } = "";
    public string? AssignedTo { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? Note { get; set; }
    public List<ManagerTransferLine> Lines { get; set; } = [];
    public List<ManagerTransferDocument> Dispatches { get; set; } = [];
    public List<ManagerTransferDocument> Receipts { get; set; } = [];
}

public record ManagerTransferLine(
    string ProductCode, string ProductName, long Requested, long Cancelled,
    long Dispatched, long Received, long InTransit)
{
    public long Outstanding => Requested - Cancelled - Dispatched;
}

public record ManagerTransferDocument(string Code, DateOnly Date, string Status, string? StaffName);
