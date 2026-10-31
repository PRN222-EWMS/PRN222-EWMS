using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class WarehouseListFilter : IValidatableObject
{
    [Display(Name = "Tên hoặc ID kho")]
    [StringLength(150, ErrorMessage = "Từ khóa tìm kiếm không được vượt quá 150 ký tự.")]
    public string? Search { get; set; }

    [Display(Name = "Loại kho")]
    [RegularExpression("MAIN|RENTED", ErrorMessage = "Loại kho không hợp lệ.")]
    public string? WarehouseType { get; set; }

    [Display(Name = "Trạng thái")]
    [RegularExpression("ACTIVE|INACTIVE", ErrorMessage = "Trạng thái kho không hợp lệ.")]
    public string? Status { get; set; }

    [Display(Name = "Thuê từ ngày"), DataType(DataType.Date)]
    public DateOnly? RentalFrom { get; set; }

    [Display(Name = "Thuê đến ngày"), DataType(DataType.Date)]
    public DateOnly? RentalTo { get; set; }

    public int Page { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RentalFrom.HasValue && RentalTo.HasValue && RentalFrom > RentalTo)
            yield return new ValidationResult("Ngày bắt đầu không được sau ngày kết thúc.", [nameof(RentalTo)]);
    }
}
