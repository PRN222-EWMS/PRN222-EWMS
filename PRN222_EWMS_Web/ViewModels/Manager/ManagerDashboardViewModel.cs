using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Manager;

public class ManagerDashboardViewModel
{
    public int? WarehouseId { get; set; }

    public List<SelectListItem> Warehouses { get; set; } = new();

    public int PendingInboundCount { get; set; }
    public int PendingOutboundCount { get; set; }
    public int OpenPurchaseOrderCount { get; set; }
    public int ActiveStocktakeCount { get; set; }

    public List<DashboardStatusItem> PurchaseStatuses { get; set; } = new();
    public List<DashboardStatusItem> SalesStatuses { get; set; } = new();

    public List<DashboardReceiptItem> RecentReceipts { get; set; } = new();

    public List<VwLocationCapacity> Capacities { get; set; } = new();

    public List<WarehouseCapacitySummary> WarehouseCapacities { get; set; } = new();
}

public class WarehouseCapacitySummary
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public decimal TotalAreaM2 { get; set; }
    public decimal UsedAreaM2 { get; set; }
    public decimal TotalVolumeM3 { get; set; }
    public decimal UsedVolumeM3 { get; set; }
    public int LocationCount { get; set; }
    public int OverloadedLocationCount { get; set; }
    public int NearFullLocationCount { get; set; }
    public int MissingCapacityLocationCount { get; set; }
}

public class DashboardStatusItem
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class DashboardReceiptItem
{
    public string Type { get; set; } = "";
    public string Code { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
