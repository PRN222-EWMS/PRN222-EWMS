using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Authentication;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";
}
