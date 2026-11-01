using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Customer;

public class CreateCustomerViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên khách hàng.")]
    [StringLength(200, ErrorMessage = "Tên khách hàng tối đa 200 ký tự.")]
    [Display(Name = "Tên khách hàng")]
    public string CustomerName { get; set; } = "";

    [StringLength(50, ErrorMessage = "Tên viết tắt tối đa 50 ký tự.")]
    [Display(Name = "Tên viết tắt")]
    public string? Nickname { get; set; }

    [StringLength(254, ErrorMessage = "Email tối đa 254 ký tự.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    public bool ConfirmDuplicatePhone { get; set; }
    public bool HasDuplicatePhoneWarning { get; set; }
    public CustomerListFilter ReturnFilter { get; set; } = new();
}
