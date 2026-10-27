using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class CreateAccountViewModel
{
    [Required(ErrorMessage = "Nhập tên đăng nhập."), StringLength(100)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Chỉ dùng chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Nhập họ tên."), StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(254)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ."), StringLength(30)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu."), StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 128 ký tự."), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Xác nhận mật khẩu."), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp."), DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public int RoleId { get; set; }

    [Display(Name = "Kho làm việc")]
    public int? WarehouseId { get; set; }

    [ValidateNever]
    public List<SelectListItem> Roles { get; set; } = [];
    [ValidateNever]
    public List<SelectListItem> Warehouses { get; set; } = [];
}
