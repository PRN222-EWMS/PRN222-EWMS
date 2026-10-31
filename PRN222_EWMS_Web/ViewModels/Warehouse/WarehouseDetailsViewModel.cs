namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class WarehouseDetailsViewModel
{
    public WarehouseListItemViewModel Warehouse { get; init; } = new();
    public List<StorageLocationViewModel> Zones { get; init; } = [];
    public List<StorageLocationViewModel> UnlinkedLocations { get; init; } = [];
    public int LocationCount { get; init; }
    public int RowCount { get; init; }
    public int RackCount { get; init; }
    public int TierCount { get; init; }
    public int FloorSlotCount { get; init; }
    public decimal RackVolumeM3 { get; init; }
    public decimal FloorAreaM2 { get; init; }
    public bool HasStoredItems { get; init; }
    public List<string> SetupIssues { get; init; } = [];
}
