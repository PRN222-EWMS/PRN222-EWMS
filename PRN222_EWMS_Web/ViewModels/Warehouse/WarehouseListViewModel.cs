namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class WarehouseListViewModel : WarehouseListFilter
{
    public const int DefaultPageSize = 10;

    public WarehouseListViewModel(WarehouseListFilter filter)
    {
        Search = filter.Search;
        WarehouseType = filter.WarehouseType;
        Status = filter.Status;
        RentalFrom = filter.RentalFrom;
        RentalTo = filter.RentalTo;
        Page = Math.Max(1, filter.Page);
    }

    public IReadOnlyList<WarehouseListItemViewModel> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalWarehouses { get; init; }
    public int TotalPages => TotalCount == 0 ? 1 : (TotalCount - 1) / DefaultPageSize + 1;
    public bool DataLoaded { get; init; }
    public string? LoadError { get; init; }
}
