namespace PRN222_EWMS_Web.Models;

public class StorageLocation
{
    public int LocationId { get; set; }
    public int WarehouseId { get; set; }
    public int? ParentLocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string LocationType { get; set; } = "";
    public string? ZoneType { get; set; }
    public string? StorageType { get; set; }
    public string? ConditionPolicy { get; set; }
    public string? CapacityMode { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? MaxWeightKg { get; set; }
    public string Status { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}
