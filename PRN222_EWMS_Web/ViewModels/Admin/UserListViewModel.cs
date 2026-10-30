using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class UserListFilter
{
    [StringLength(254)]
    public string? Search { get; set; }

    public int? RoleId { get; set; }

    public string? Status { get; set; }

    public int? WarehouseId { get; set; }

    public int Page { get; set; } = 1;
}

public class UserListViewModel
{
    public string? Search { get; set; }

    public int? RoleId { get; set; }

    public string? Status { get; set; }

    public int? WarehouseId { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<UserListItemViewModel> Accounts { get; set; } = [];

    public List<SelectListItem> Roles { get; set; } = [];

    public List<SelectListItem> Warehouses { get; set; } = [];

    public List<SelectListItem> Statuses { get; } =
    [
        new("Đang hoạt động", "ACTIVE"),
        new("Bị khóa", "LOCKED"),
        new("Ngừng hoạt động", "INACTIVE")
    ];

    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || RoleId.HasValue || !string.IsNullOrWhiteSpace(Status) || WarehouseId.HasValue;

    public int FirstItemNumber => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemNumber => Math.Min(Page * PageSize, TotalCount);
}

public class UserListItemViewModel
{
    public int UserId { get; set; }

    public string FullName { get; set; } = "";

    public string? Email { get; set; }

    public string RoleName { get; set; } = "";

    public string? WarehouseName { get; set; }

    public string StatusLabel { get; set; } = "";

    public string StatusClass { get; set; } = "unknown";

    public DateTime CreatedAt { get; set; }
}
