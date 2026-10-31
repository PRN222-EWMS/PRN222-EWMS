using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Supplier;

public class CreateSupplierViewModel
{
    [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên nhà cung cấp không được vượt quá 200 ký tự.")]
    [Display(Name = "Tên nhà cung cấp")]
    public string SupplierName { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Tên gợi nhớ không được vượt quá 50 ký tự.")]
    [Display(Name = "Tên gợi nhớ (Nickname)")]
    public string? Nickname { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [StringLength(30)]
    [RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại không hợp lệ (phải gồm đúng 10 chữ số và bắt đầu bằng số 0).")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    public bool ConfirmDuplicatePhone { get; set; }

    public bool HasDuplicatePhoneWarning { get; set; }
}

