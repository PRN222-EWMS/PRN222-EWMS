using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Authentication;

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = "";

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
