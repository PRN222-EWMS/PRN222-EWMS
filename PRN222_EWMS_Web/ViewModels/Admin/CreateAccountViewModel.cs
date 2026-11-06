using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class CreateAccountViewModel
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(254)]
    public string Email { get; set; } = "";

    [RegularExpression(AccountValidation.PhonePattern, ErrorMessage = AccountValidation.PhoneMessage)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu."), StringLength(128, MinimumLength = 11, ErrorMessage = AccountValidation.PasswordLengthMessage), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    [RegularExpression(AccountValidation.PasswordPattern, ErrorMessage = AccountValidation.PasswordComplexityMessage)]
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
