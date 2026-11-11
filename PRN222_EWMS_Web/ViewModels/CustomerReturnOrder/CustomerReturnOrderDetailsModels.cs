using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

public record ReturnReceiptHistoryDto(
    long ReturnReceiptId,
    string ReceiptCode,
    DateOnly ReceiptDate,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? PerformedByName,
    int ActiveItemCount,
    string? Note
);

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
    public long SalesOrderId { get; set; }
    public string SalesOrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    // 3. Receiving Progress Summary
    public int TotalRequestedQuantity { get; set; }
    public int TotalCancelledQuantity { get; set; }
    public int TotalReceivedQuantity { get; set; }
    public int TotalRemainingQuantity { get; set; }
    public int ProgressPercentage { get; set; }

    // 4. Role Affordances & Action Navigation
    public bool IsWarehouseStaff { get; set; }
    public bool CanEdit { get; set; }
    public bool CanReceive { get; set; }

    // 5. Line Items & Receipt History
    public List<ReturnOrderDetailLineViewModel> Lines { get; set; } = [];
    public List<ReturnReceiptHistoryItemViewModel> Receipts { get; set; } = [];
}

public class ReturnOrderDetailLineViewModel
{
    public long ReturnLineId { get; set; }
    public long SolineId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int RequestedQuantity { get; set; }
    public int CancelledQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
}

public class ReturnReceiptHistoryItemViewModel
{
    public long ReturnReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public DateOnly ReceiptDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? PerformedByName { get; set; }
    public int ActiveItemCount { get; set; }
    public string? Note { get; set; }
}

