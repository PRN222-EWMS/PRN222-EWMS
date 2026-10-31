namespace PRN222_EWMS_Web.Services.Warehouse;

public class WarehouseSaveResult
{
    public int? WarehouseId { get; init; }
    public int? LocationId { get; init; }
    public bool NotFound { get; init; }
    public bool Conflict { get; init; }
    public Dictionary<string, string> Errors { get; init; } = [];
}
