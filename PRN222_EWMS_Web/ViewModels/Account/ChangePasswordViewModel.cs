using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Account;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại."), StringLength(128), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(128, MinimumLength = 11, ErrorMessage = AccountValidation.PasswordLengthMessage)]
    [DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
    [RegularExpression(AccountValidation.PasswordPattern, ErrorMessage = AccountValidation.PasswordComplexityMessage)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = "";
}
