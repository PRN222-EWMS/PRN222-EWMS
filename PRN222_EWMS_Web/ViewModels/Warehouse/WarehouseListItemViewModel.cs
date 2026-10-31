namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class WarehouseListItemViewModel
{
    public int WarehouseId { get; init; }
    public string WarehouseName { get; init; } = "";
    public string Address { get; init; } = "";
    public string WarehouseType { get; init; } = "";
    public DateOnly? RentalStartDate { get; init; }
    public DateOnly? RentalEndDate { get; init; }
    public string Status { get; init; } = "";

    public string TypeLabel => WarehouseType switch
    {
        "MAIN" => "Kho chính",
        "RENTED" => "Kho thuê",
        _ => string.IsNullOrWhiteSpace(WarehouseType) ? "Chưa xác định" : WarehouseType
    };

    public string StatusLabel => Status switch
    {
        "ACTIVE" => "Đang hoạt động",
        "INACTIVE" => "Ngừng hoạt động",
        _ => string.IsNullOrWhiteSpace(Status) ? "Chưa xác định" : Status
    };
}
