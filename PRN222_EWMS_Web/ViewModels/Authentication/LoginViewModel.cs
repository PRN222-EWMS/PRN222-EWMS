using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Authentication;

public class LoginViewModel
{
    [Required(ErrorMessage = "Nhập tên đăng nhập."), StringLength(100)]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu."), StringLength(128), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
