using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Account;

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(254)]
    public string? Email { get; set; }

    [RegularExpression(AccountValidation.PhonePattern, ErrorMessage = AccountValidation.PhoneMessage)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }
}
