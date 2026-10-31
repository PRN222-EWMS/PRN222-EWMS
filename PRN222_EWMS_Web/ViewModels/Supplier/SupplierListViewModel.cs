using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Supplier;

public class SupplierListFilter
{
    [StringLength(254)]
    public string? Search { get; set; }

    public string? Status { get; set; } = "ACTIVE";

    public int Page { get; set; } = 1;
}

public class SupplierListItemViewModel
{
    public int SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    public string? Nickname { get; set; }

    public string DisplayName => !string.IsNullOrWhiteSpace(Nickname)
        ? $"{SupplierName} ({Nickname})"
        : SupplierName;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string Status { get; set; } = null!;

    public string StatusLabel => Status == "ACTIVE" ? "Đang hoạt động" : "Ngừng hoạt động";

    public string StatusClass => Status == "ACTIVE" ? "status-badge status-active" : "status-badge status-inactive";

    public bool IsActive => Status == "ACTIVE";
}

public class SupplierListViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; } = "ACTIVE";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<SupplierListItemViewModel> Suppliers { get; set; } = [];

    public List<SelectListItem> Statuses { get; } =
    [
        new("Đang hoạt động", "ACTIVE"),
        new("Ngừng hoạt động", "INACTIVE"),
        new("Tất cả", "ALL")
    ];

    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || (Status != null && Status != "ACTIVE");

    public int FirstItemNumber => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemNumber => Math.Min(Page * PageSize, TotalCount);
}

