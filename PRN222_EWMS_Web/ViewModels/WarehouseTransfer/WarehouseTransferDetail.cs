namespace PRN222_EWMS_Web.ViewModels.WarehouseTransfer;

public class WarehouseTransferDetail
{
    public long Id { get; set; }
    public bool CanClaimDispatch { get; set; }
    public bool CanClaimReceipt { get; set; }
    public string Code { get; set; } = "";
    public string FromWarehouse { get; set; } = "";
    public string ToWarehouse { get; set; } = "";
    public string Status { get; set; } = "";
    public string ItemCondition { get; set; } = "";
    public bool IsSource { get; set; }
    public List<TransferProgressRow> Lines { get; set; } = [];
    public List<WarehouseTransferDocument> Documents { get; set; } = [];
    public List<string> IncomingSerials { get; set; } = [];
    public List<DispatchSerialOption> DispatchSerialOptions { get; set; } = [];
    public Dictionary<int, string> PickLocations { get; set; } = [];
    public bool IsDispatchQuantityCovered { get; set; }
}

public record DispatchSerialOption(string ProductCode, string ProductName, string Location, string SerialNumber,
    byte? ManufacturingMonth, short? ManufacturingYear, string ItemCondition)
{
    public string ManufacturingDateDisplay => (ManufacturingYear, ManufacturingMonth) switch
    {
        ({ } year, { } month) => $"{month:D2}/{year}",
        ({ } year, null) => year.ToString(),
        _ => "Chưa có dữ liệu"
    };
}

public record TransferProgressRow(
    string ProductCode, string ProductName, int Requested, int Cancelled,
    int Dispatched, int Received, int InTransit);

public record WarehouseTransferDocument(
    string Code, DateOnly Date, string Status, string? StaffName,
    List<WarehouseTransferSerial> Serials)
{
    public long? DispatchId { get; init; }
    public bool CanScan { get; init; }
    public long? ReceiptId { get; init; }
    public bool CanScanReceipt { get; init; }
}

public record WarehouseTransferSerial(string ProductCode, string SerialNumber, string? Condition)
{
    public long ItemId { get; init; }
    public int? LocationId { get; init; }
    public string? ConditionNote { get; init; }
}
