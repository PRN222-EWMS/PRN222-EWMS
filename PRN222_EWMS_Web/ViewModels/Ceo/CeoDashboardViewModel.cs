using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Ceo;

public class CeoDashboardFilter
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn kho hợp lệ.")]
    public int? WarehouseId { get; set; }

    [Range(7, 90, ErrorMessage = "Khoảng theo dõi phải từ 7 đến 90 ngày.")]
    public int Days { get; set; } = 30;
}

public class CeoDashboardViewModel
{
    public CeoDashboardFilter Filter { get; set; } = new();
    public List<CeoDashboardWarehouseOption> WarehouseOptions { get; set; } = [];
    public List<CeoDashboardWarehouse> Warehouses { get; set; } = [];
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Error { get; set; }
    public CeoDashboardWorkload Workload { get; set; } = new();
    public List<CeoDashboardDay> Trend { get; set; } = [];
    public List<CeoDashboardActivity> RecentActivity { get; set; } = [];
    public List<CeoDashboardApproval> PendingApprovals { get; set; } = [];
    public List<CeoDashboardAttention> Attention { get; set; } = [];

    public int OperationalWarehouses => Warehouses.Count(w => w.IsOperational);
    public long InventoryCount => Warehouses.Sum(w => w.InventoryCount);
    public long GoodCount => Warehouses.Sum(w => w.GoodCount);
    public long DamagedCount => Warehouses.Sum(w => w.DamagedCount);
    public long AwaitingCount => Warehouses.Sum(w => w.AwaitingCount);
    public long IncomingCount => Warehouses.Sum(w => w.IncomingPurchase + w.IncomingTransfer + w.IncomingReturn);
}

public record CeoDashboardWarehouseOption(int Id, string Name);

public class CeoDashboardWarehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly? RentalEndDate { get; set; }
    public int? LeaseDaysLeft { get; set; }
    public bool IsOperational { get; set; }
    public long InventoryCount { get; set; }
    public long GoodCount { get; set; }
    public long DamagedCount { get; set; }
    public long AwaitingCount { get; set; }
    public long IncomingPurchase { get; set; }
    public long IncomingTransfer { get; set; }
    public long IncomingReturn { get; set; }
    public decimal UsedFloorArea { get; set; }
    public decimal TotalFloorArea { get; set; }
    public decimal UsedRackVolume { get; set; }
    public decimal TotalRackVolume { get; set; }
    public int ZoneCount { get; set; }
    public int LocationCount { get; set; }
    public int NearFullCount { get; set; }
    public int OverloadedCount { get; set; }
    public int InvalidLocationCount { get; set; }
    public int ActiveStocktakes { get; set; }
    public int PendingApprovalCount { get; set; }
}

public class CeoDashboardWorkload
{
    public int OpenPurchaseOrders { get; set; }
    public long RemainingPurchaseQuantity { get; set; }
    public int OpenSalesOrders { get; set; }
    public long RemainingSalesQuantity { get; set; }
    public int OpenReturns { get; set; }
    public long RemainingReturnQuantity { get; set; }
    public int OpenTransfers { get; set; }
    public long RemainingTransferQuantity { get; set; }
    public long InTransitQuantity { get; set; }
    public int DraftInboundCount { get; set; }
    public int OpenOutboundCount { get; set; }
    public int ActiveStocktakes { get; set; }
    public int PendingApprovalCount { get; set; }
}

public class CeoDashboardDay
{
    public DateOnly Day { get; set; }
    public long Purchased { get; set; }
    public long Sold { get; set; }
    public long Returned { get; set; }
    public long TransferReceived { get; set; }
    public long TransferDispatched { get; set; }
    public long Incoming => Purchased + Returned + TransferReceived;
    public long Outgoing => Sold + TransferDispatched;
}

public record CeoDashboardActivity(string Type, string Code, string Warehouse, long Quantity, DateTime OccurredAt);
public record CeoDashboardApproval(long Id, string Code, string StocktakeCode, string Warehouse, long Quantity, DateTime? SubmittedAt);
public record CeoDashboardAttention(string Tone, string Title, string Description, int? WarehouseId = null);
