using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Supplier;

public class EditSupplierViewModel : CreateSupplierViewModel
{
    [Required]
    public int SupplierId { get; set; }

    [Required]
    [RegularExpression(@"^(ACTIVE|INACTIVE)$", ErrorMessage = "Trạng thái không hợp lệ.")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "ACTIVE";

    public SupplierListFilter ReturnFilter { get; set; } = new();
}
