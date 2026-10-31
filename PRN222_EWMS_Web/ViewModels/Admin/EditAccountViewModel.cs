using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class EditAccountViewModel
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Nhập họ tên."), StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(254)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ."), StringLength(30)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public int RoleId { get; set; }

    [Required(ErrorMessage = "Chọn trạng thái.")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "";

    [Display(Name = "Kho làm việc")]
    public int? WarehouseId { get; set; }

    [Required]
    public string RowVersion { get; set; } = "";

    public UserListFilter ReturnFilter { get; set; } = new();

    [ValidateNever]
    public List<SelectListItem> Roles { get; set; } = [];

    [ValidateNever]
    public List<SelectListItem> Statuses { get; set; } = [];

    [ValidateNever]
    public List<SelectListItem> Warehouses { get; set; } = [];
}
