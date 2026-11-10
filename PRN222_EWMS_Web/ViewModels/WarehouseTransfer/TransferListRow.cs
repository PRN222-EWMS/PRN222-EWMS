namespace PRN222_EWMS_Web.ViewModels.WarehouseTransfer;

public sealed record TransferListRow(
    long Id,
    string Code,
    string FromWarehouse,
    string ToWarehouse,
    bool IsSource,
    string Status,
    int Dispatched,
    int Received,
    int InTransit);
