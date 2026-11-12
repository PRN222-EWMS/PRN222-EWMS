using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.ViewModels.Putaway;

public class PutawayFilter
{
    [Required(ErrorMessage = "Vui lòng chọn nguồn hàng."), RegularExpression("PURCHASE|RETURN|TRANSFER_RECEIPT", ErrorMessage = "Nguồn hàng không hợp lệ.")]
    public string Source { get; set; } = "PURCHASE";
    [Range(1, long.MaxValue)] public long? ReceiptId { get; set; }
    [StringLength(100)] public string? Search { get; set; }
    [RegularExpression("GOOD|DAMAGED")] public string? Condition { get; set; }
    [Range(1, 1_000_000)] public int Page { get; set; } = 1;
}

public class PutawayIndexViewModel
{
    public string WarehouseName { get; set; } = "";
    public PutawayQueueViewModel Queue { get; set; } = new();
    public string? LoadError { get; set; }
}

public class PutawayQueueViewModel
{
    public const int PageSize = 10;
    public PutawayFilter Filter { get; set; } = new();
    public List<PutawayReceiptOption> Receipts { get; set; } = [];
    public List<PutawayPendingItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public string SourceLabel => Filter.Source switch
    {
        "RETURN" => "Hàng khách trả",
        "TRANSFER_RECEIPT" => "Hàng chuyển kho",
        _ => "Hàng mua"
    };
    public string ReceiptLabel => Filter.Source switch
    {
        "RETURN" => "Phiếu nhận hàng trả đã hoàn tất",
        "TRANSFER_RECEIPT" => "Phiếu nhận chuyển kho đã hoàn tất",
        _ => "Phiếu nhập mua đã hoàn tất"
    };
}

public record PutawayReceiptOption(long Id, string Code, int PendingCount,
    string? TransferCode = null, string? FromWarehouseName = null);
public record PutawayPendingItem(long Id, string Serial, string ProductCode, string ProductName,
    string Category, string Brand, string Condition, bool Quarantined, long ReceiptId,
    string ReceiptCode, DateTime ReceivedAt, long? TransferOrderId = null,
    string? TransferCode = null, string? FromWarehouseName = null);

public class PutawaySerialViewModel
{
    public long Id { get; init; }
    public string Serial { get; init; } = "";
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string Category { get; init; } = "";
    public string Brand { get; init; } = "";
    public string WarehouseName { get; init; } = "";
    public string Source { get; init; } = "PURCHASE";
    public string SourceLabel => Source switch
    {
        "RETURN" => "Hàng khách trả",
        "TRANSFER_RECEIPT" => "Hàng chuyển kho",
        _ => "Hàng mua"
    };
    public long? ReceiptId { get; init; }
    public string ReceiptCode { get; init; } = "";
    public long? TransferOrderId { get; init; }
    public string? TransferCode { get; init; }
    public string? FromWarehouseName { get; init; }
    public string Condition { get; init; } = "";
    public bool Quarantined { get; init; }
    public decimal LengthCm { get; init; }
    public decimal WidthCm { get; init; }
    public decimal HeightCm { get; init; }
    public decimal WeightKg { get; init; }
    public string RowVersion { get; init; } = "";
    public string? CurrentLocationPath { get; init; }
    public string? UnavailableReason { get; set; }
    public List<PutawayLocationViewModel> Locations { get; set; } = [];
    public string? EmptyReason { get; set; }
}

public class PutawayLocationViewModel
{
    public int Id { get; init; }
    public string Path { get; init; } = "";
    public int ZoneId { get; init; }
    public string Zone { get; init; } = "";
    public int RowId { get; init; }
    public string Row { get; init; } = "";
    public int? RackId { get; init; }
    public string? Rack { get; init; }
    public string Name { get; init; } = "";
    public bool IsReserve { get; init; }
    public bool IsFloor { get; init; }
    public decimal LengthCm { get; init; }
    public decimal WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal TotalCapacity { get; init; }
    public decimal UsedCapacity { get; init; }
    public decimal RemainingCapacity => TotalCapacity - UsedCapacity;
    public string CapacityUnit => IsFloor ? "m²" : "m³";
    public decimal UsedWeightKg { get; init; }
    public decimal? MaxWeightKg { get; init; }
    public decimal? RemainingWeightKg => MaxWeightKg - UsedWeightKg;
    public int OccupantCount { get; init; }
}

public class ConfirmPutawayInput
{
    [Range(1, long.MaxValue)] public long SerialItemId { get; set; }
    [Range(1, int.MaxValue)] public int TargetLocationId { get; set; }
    [Required, StringLength(32)] public string RowVersion { get; set; } = "";
}

public record PutawayResult(string Serial, string Path, string Source, long? ReceiptId);
public record PutawayActor(int UserId, int WarehouseId, string WarehouseName);
public record PutawayOccupant(long SerialId, int WarehouseId, int LocationId, string Condition,
    decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal WeightKg);
public record PutawayLayout(WarehouseEntity Warehouse, List<StorageLocation> Locations,
    List<ZoneStorageRule> Rules, bool AwaitingLocked, HashSet<int> LockedLocations);

public class PutawayException(string message, int statusCode = 409) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
