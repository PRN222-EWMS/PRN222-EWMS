using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Manager;

public static class ManagerStocktakeLabels
{
    public static readonly string[] Statuses = ["DRAFT", "PLANNED", "COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL", "COMPLETED", "CANCELLED"];
    public static readonly string[] Differences = ["PENDING", "MATCHED", "MISSING", "SURPLUS", "DAMAGED", "WRONG_LOCATION", "COMBINED"];
    public static string Status(string value) => value switch
    {
        "DRAFT" => "Nháp", "PLANNED" => "Đã lập kế hoạch", "COUNTING" => "Đang đếm",
        "RECOUNTING" => "Đang đếm lại", "REVIEWING" => "Đang rà soát", "PENDING_APPROVAL" => "Chờ duyệt",
        "COMPLETED" => "Hoàn tất", "CANCELLED" => "Đã hủy", "ASSIGNED" => "Đã giao",
        "IN_PROGRESS" => "Đang thực hiện", "DONE" => "Đã hoàn thành", "OPEN" => "Đang mở", "CLOSED" => "Đã đóng", _ => value
    };
    public static string Difference(string value) => value switch
    {
        "PENDING" => "Chờ đếm", "MATCHED" => "Khớp", "MISSING" => "Thiếu", "SURPLUS" => "Dư",
        "DAMAGED" => "Sai tình trạng", "WRONG_LOCATION" => "Sai vị trí", "COMBINED" => "Nhiều sai lệch", _ => value
    };
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
    public static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
}

public class ManagerStocktakeFilter
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
}

public record ManagerStocktakeWarehouseOption(int Id, string Name, string Type, string Status, DateOnly? RentalStartDate, DateOnly? RentalEndDate)
{
    public bool CanPlan(DateOnly date) => Status == "ACTIVE" && (Type == "MAIN"
        || (Type == "RENTED" && RentalStartDate.HasValue && RentalEndDate.HasValue
            && RentalStartDate.Value <= date && RentalEndDate.Value >= date));
}

public class ManagerStocktakeListViewModel
{
    public const int PageSize = 15;
    public ManagerStocktakeFilter Filter { get; set; } = new();
    public List<ManagerStocktakeWarehouseOption> Warehouses { get; set; } = [];
    public List<ManagerStocktakeListItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}

public class ManagerStocktakeListItem
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public string Status { get; set; } = "";
    public string ScopeType { get; set; } = "";
    public bool IncludeAwaitingPutaway { get; set; }
    public DateTime? PlannedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public int ScopeCount { get; set; }
    public int StaffCount { get; set; }
    public int AssignmentCount { get; set; }
    public int CompletedAssignmentCount { get; set; }
    public int? LatestRoundNumber { get; set; }
    public int ExpectedCount { get; set; }
    public int ScannedCount { get; set; }
    public bool CanEdit { get; set; }
}

public class ManagerStocktakeAssignmentInput
{
    public int UserId { get; set; }
    [StringLength(16)]
    public string ScopeKind { get; set; } = "LOCATION";
    public int? LocationId { get; set; }
}

public class ManagerStocktakeFormViewModel
{
    public long? Id { get; set; }
    public string? Code { get; set; }
    public string? RowVersion { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn kho kiểm kê.")]
    public int WarehouseId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn loại phạm vi.")]
    [StringLength(16)]
    public string ScopeType { get; set; } = "WHOLE_WAREHOUSE";
    public bool IncludeAwaitingPutaway { get; set; } = true;
    [DataType(DataType.DateTime)]
    public DateTime? PlannedAt { get; set; }
    [StringLength(1000, ErrorMessage = "Ghi chú tối đa 1.000 ký tự.")]
    public string? Note { get; set; }
    public string SaveMode { get; set; } = "DRAFT";
    public List<int> ScopeLocationIds { get; set; } = [];
    public List<ManagerStocktakeAssignmentInput> Assignments { get; set; } = [];
    public List<ManagerStocktakeWarehouseOption> Warehouses { get; set; } = [];
    public List<ManagerStocktakeLocationOption> Locations { get; set; } = [];
    public List<ManagerStocktakeStaffOption> Staff { get; set; } = [];
    public int AwaitingCount { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class ManagerStocktakeLocationOption
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public string Path { get; set; } = "";
    public int Depth { get; set; }
}

public record ManagerStocktakeStaffOption(int Id, string Name);
public record ManagerStocktakePlanningData(ManagerStocktakeWarehouseOption Warehouse, List<ManagerStocktakeLocationOption> Locations,
    List<ManagerStocktakeStaffOption> Staff, int AwaitingCount);

public class ManagerStocktakeDetailFilter
{
    public int? RoundNumber { get; set; }
    public string? Search { get; set; }
    public string? DifferenceType { get; set; }
    public int Page { get; set; } = 1;
}

public class ManagerStocktakeDetailViewModel
{
    public const int PageSize = 30;
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public string Creator { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public string ScopeType { get; set; } = "";
    public bool IncludeAwaitingPutaway { get; set; }
    public DateTime? PlannedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Note { get; set; }
    public string RowVersion { get; set; } = "";
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public List<string> ScopePaths { get; set; } = [];
    public List<ManagerStocktakeAssignmentView> Assignments { get; set; } = [];
    public List<ManagerStocktakeRoundView> Rounds { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public DateTime? OriginalCapturedAt { get; set; }
    public ManagerStocktakeDetailFilter Filter { get; set; } = new();
    public int ExpectedCount { get; set; }
    public int ScannedCount { get; set; }
    public int DiscrepancyCount { get; set; }
    public List<ManagerStocktakeObservationView> Observations { get; set; } = [];
    public int ObservationCount { get; set; }
    public int TotalPages => Math.Max(1, (ObservationCount + PageSize - 1) / PageSize);
}

public record ManagerStocktakeAssignmentView(long Id, int UserId, string Name, string ScopeKind, string Scope, string Status, DateTime AssignedAt);
public record ManagerStocktakeRoundView(int Number, string Status, DateTime StartedAt, DateTime? ClosedAt, string StartedBy, string? RecountReason);
public record ManagerStocktakeObservationView(long Id, string Serial, string? Product, int ExpectedQuantity, int ActualQuantity,
    string? ExpectedCondition, string? ActualCondition, string ExpectedLocation, string ActualLocation, string DifferenceType,
    DateTime CapturedAt, DateTime? ScannedAt, string? ScannedBy, string ReviewStatus, string? Note);

public class ManagerStocktakeDeleteInput
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.")]
    public string RowVersion { get; set; } = "";
}
