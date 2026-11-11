using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

public class ReturnOrderListFilterViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool Mine { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ReturnOrderListViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? WarehouseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool Mine { get; set; } = false;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public bool CanFilterMine { get; set; }
    public bool CanCreate { get; set; }
    public bool IsWarehouseStaff { get; set; }
    public string? AssignedWarehouseName { get; set; }
    public string? ScopeMessage { get; set; }

    public List<WarehouseOptionDto> Warehouses { get; set; } = [];
    public List<ReturnOrderListRowViewModel> Items { get; set; } = [];
}

public class ReturnOrderListRowViewModel
{
    public long Id { get; set; }
    public string ReturnCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = "";
    public string Status { get; set; } = "";
    public int TotalRequestedQuantity { get; set; }
    public int CompletedReceivedQuantity { get; set; }
    public int RemainingQuantity => Status is "CANCELLED" or "CLOSED"
        ? 0
        : Math.Max(0, TotalRequestedQuantity - CompletedReceivedQuantity);
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReceive { get; set; }
}

public record ReturnOrderFilterDto(
    string? Search,
    string? Status,
    int? WarehouseId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int? CreatedByUserId,
    bool ExcludeDraft,
    int Skip,
    int Take
);

public record ReturnOrderListRowDto(
    long ReturnOrderId,
    string ReturnCode,
    string CustomerName,
    string WarehouseName,
    DateTime CreatedAt,
    string CreatedByName,
    string Status,
    int TotalRequestedQuantity,
    int CompletedReceivedQuantity,
    bool HasReturnReceipts
);

