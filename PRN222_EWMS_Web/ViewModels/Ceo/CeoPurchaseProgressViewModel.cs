namespace PRN222_EWMS_Web.ViewModels.Ceo;

public class CeoPurchaseProgressFilter
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public int Page { get; set; } = 1;
}

public class CeoPurchaseProgressViewModel
{
    public CeoPurchaseProgressFilter Filter { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public long Ordered { get; set; }
    public long Received { get; set; }
    public long Outstanding { get; set; }
    public long Closed { get; set; }
    public List<CeoPurchaseOrderRow> Orders { get; set; } = [];
    public List<CeoLookupOption> Warehouses { get; set; } = [];
    public List<CeoLookupOption> Suppliers { get; set; } = [];
}

public record CeoLookupOption(int Id, string Name);
public record CeoPurchaseOrderRow(long Id, string Code, string Supplier, string Warehouse, string Status,
    DateOnly OrderDate, DateOnly ExpectedArrivalDate, long Ordered, long Received, long Outstanding, long Closed);

public class CeoPurchaseOrderDetailViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly OrderDate { get; set; }
    public DateOnly ExpectedArrivalDate { get; set; }
    public long Ordered { get; set; }
    public long Received { get; set; }
    public long Outstanding { get; set; }
    public long Closed { get; set; }
    public List<CeoPurchaseLineRow> Lines { get; set; } = [];
}

public record CeoPurchaseLineRow(string ProductCode, string ProductName, long Ordered, long Received, long Outstanding, long Closed);
