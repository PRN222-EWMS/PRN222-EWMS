using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

public class ReturnOrderDetailViewModel
{
    // 1. Order Identity & Status
    public long ReturnOrderId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Reason { get; set; }
    public string? CloseReason { get; set; }
    public DateTime? ClosedAt { get; set; }

    // 2. Origin & Warehouse Scoping
    public string SalesOrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;

    // 3. Receiving Progress Summary
    public int TotalCancelledQuantity { get; set; }

    // 4. Role Affordances & Action Navigation
    public bool CanEdit { get; set; }
    public bool CanReceive { get; set; }

    // 5. Line Items
    public List<ReturnOrderDetailLineViewModel> Lines { get; set; } = [];
}

public class ReturnOrderDetailLineViewModel
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int RequestedQuantity { get; set; }
    public int CancelledQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
}
