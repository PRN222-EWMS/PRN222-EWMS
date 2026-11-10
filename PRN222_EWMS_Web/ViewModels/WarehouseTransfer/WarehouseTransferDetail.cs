namespace PRN222_EWMS_Web.ViewModels.WarehouseTransfer;

public class WarehouseTransferDetail
{
    public string Code { get; set; } = "";
    public string FromWarehouse { get; set; } = "";
    public string ToWarehouse { get; set; } = "";
    public string Status { get; set; } = "";
    public string ItemCondition { get; set; } = "";
    public bool IsSource { get; set; }
    public List<TransferProgressRow> Lines { get; set; } = [];
    public List<WarehouseTransferDocument> Documents { get; set; } = [];
    public List<string> IncomingSerials { get; set; } = [];
}

public record TransferProgressRow(
    string ProductCode, string ProductName, int Requested, int Cancelled,
    int Dispatched, int Received, int InTransit);

public record WarehouseTransferDocument(
    string Code, DateOnly Date, string Status, string? StaffName,
    List<WarehouseTransferSerial> Serials);

public record WarehouseTransferSerial(string ProductCode, string SerialNumber, string? Condition);
