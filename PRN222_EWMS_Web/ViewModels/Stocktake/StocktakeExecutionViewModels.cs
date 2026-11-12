using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Stocktaking;

public class StocktakeExecutionListViewModel
{
    public const int PageSize = 10;
    public List<StocktakeTaskRow> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int TotalCount { get; set; }
    public string? Search { get; set; }
}

public class StocktakeTaskRow
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? PlannedAt { get; set; }
    public bool IsRecount { get; set; }
    public bool IsShortageSearch { get; set; }
    public long? RecountRequestId { get; set; }
}

public class StocktakeExecutionViewModel
{
    public bool IsShortageSearch { get; set; }
    public Models.Stocktake Plan { get; set; } = null!;
    public List<StocktakeAssignment> Assignments { get; set; } = [];
    public List<StocktakeRecountRequest> Requests { get; set; } = [];
    public List<StocktakeRound> Rounds { get; set; } = [];
    public List<StocktakeObservation> Observations { get; set; } = [];
    public List<StocktakeHandling> Handling { get; set; } = [];
    public List<StorageLocation> Locations { get; set; } = [];
    public List<StorageLocation> CountLocations { get; set; } = [];
    public bool AllowAwaitingPutaway { get; set; }
    public List<Product> Products { get; set; } = [];
    public long? RecountRequestId { get; set; }
    public int ActorId { get; set; }
    public string Version { get; set; } = "";
    public bool CanStart { get; set; }
    public bool CanCount { get; set; }
    public bool CanSubmit { get; set; }
    public string ScopeLabel { get; set; } = "";
}

public record StocktakeSerialLookup(bool Found, int? ProductId, string? ProductName,
    int? ExpectedLocationId, string ExpectedLocation, bool AlreadyRecorded, string? BlockedReason = null);

public record StocktakeSourceReceipt(string Kind, long Id, string Code, int ProductId, DateTime? CompletedAt)
{
    public string Key => $"{Kind}:{Id}";
    public string Label => Kind switch
    {
        "INBOUND" => $"phiếu nhập {Code}",
        "RETURN" => $"phiếu nhận trả {Code}",
        _ => $"phiếu nhận chuyển {Code}"
    };
}

public class StocktakeActionInput
{
    public long Id { get; set; }
    public long? RecountRequestId { get; set; }
    [Required] public string Version { get; set; } = "";
}

public class StocktakeCountInput : StocktakeActionInput
{
    [Required, StringLength(100)] public string Serial { get; set; } = "";
    [Required] public string Condition { get; set; } = "GOOD";
    public int? LocationId { get; set; }
    public int? ProductId { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}

public class StocktakeWorkflowException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class StocktakeDisplay
{
    public static string Status(string value) => value switch
    {
        "DRAFT" => "Nháp", "PLANNED" => "Đã lập kế hoạch", "COUNTING" => "Đang kiểm kê",
        "RECOUNTING" => "Đang đếm lại", "REVIEWING" => "Chờ rà soát", "PENDING_APPROVAL" => "Chờ duyệt điều chỉnh",
        "COMPLETED" => "Hoàn tất", "CANCELLED" => "Đã hủy", "ASSIGNED" => "Được phân công",
        "IN_PROGRESS" => "Đang thực hiện", "DONE" => "Đã gửi", "REQUESTED" => "Chờ đếm lại",
        "SUBMITTED" => "Đã gửi kết quả", "OPEN" => "Đang mở", "CLOSED" => "Đã đóng",
        "APPROVED" => "Đã duyệt", "APPLIED" => "Đã áp dụng", "REJECTED" => "Bị từ chối", _ => value
    };
    public static string Difference(string value) => value switch
    {
        "PENDING" => "Chưa kết luận", "MATCHED" => "Khớp", "MISSING" => "Thiếu",
        "SURPLUS" => "Thừa", "DAMAGED" => "Khác tình trạng", "WRONG_LOCATION" => "Khác vị trí",
        "COMBINED" => "Khác tình trạng và vị trí", _ => value
    };
    public static string Time(DateTime? value) => value?.AddHours(7).ToString("dd/MM/yyyy HH:mm") ?? "—";
}
