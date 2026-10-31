namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class StorageLocationViewModel
{
    public int LocationId { get; init; }
    public int? ParentLocationId { get; init; }
    public string Name { get; init; } = "";
    public string LocationType { get; init; } = "";
    public string? ZoneType { get; init; }
    public string? StorageType { get; init; }
    public string? ConditionPolicy { get; set; }
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? MaxWeightKg { get; init; }
    public string Status { get; init; } = "";
    public List<StorageLocationViewModel> Children { get; } = [];
    public List<string> Rules { get; init; } = [];

    public decimal? VolumeM3 => LocationType == "TIER" ? LengthCm * WidthCm * HeightCm / 1_000_000m : null;
    public decimal? FloorAreaM2 => LocationType == "FLOOR_SLOT" ? LengthCm * WidthCm / 10_000m : null;
    public string TypeLabel => LocationType switch
    {
        "ZONE" => "Zone", "ROW" => "Dãy", "RACK" => "Kệ", "TIER" => "Tầng", "FLOOR_SLOT" => "Ô sàn", _ => LocationType
    };
    public string ConditionLabel => ConditionPolicy switch
    {
        "GOOD" => "Hàng tốt", "DAMAGED" => "Hàng hỏng", _ => "Chưa cấu hình"
    };
    public string StatusLabel => Status switch
    {
        "ACTIVE" => "Đang hoạt động", "INACTIVE" => "Ngừng hoạt động", _ => Status
    };
}
