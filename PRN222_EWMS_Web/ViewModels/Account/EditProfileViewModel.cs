using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Account;

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(254)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ."), StringLength(30)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }
}
