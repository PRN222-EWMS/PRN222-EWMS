using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class WarehouseFormViewModel : IValidatableObject
{
    [BindNever]
    public int WarehouseId { get; set; }

    [BindNever] public bool IsOccupied { get; set; }
    [BindNever] public bool IsLocked { get; set; }

    [Display(Name = "Tên kho")]
    [Required(ErrorMessage = "Vui lòng nhập tên kho.")]
    [StringLength(150, ErrorMessage = "Tên kho không được vượt quá 150 ký tự.")]
    public string WarehouseName { get; set; } = "";

    [Display(Name = "Địa chỉ")]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ kho.")]
    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự.")]
    public string Address { get; set; } = "";

    [Display(Name = "Loại kho")]
    [Required(ErrorMessage = "Vui lòng chọn loại kho.")]
    [RegularExpression("MAIN|RENTED", ErrorMessage = "Loại kho không hợp lệ.")]
    public string WarehouseType { get; set; } = "MAIN";

    [Display(Name = "Trạng thái")]
    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [RegularExpression("ACTIVE|INACTIVE", ErrorMessage = "Trạng thái kho không hợp lệ.")]
    public string Status { get; set; } = "ACTIVE";

    [Display(Name = "Ngày bắt đầu thuê"), DataType(DataType.Date)]
    public DateOnly? RentalStartDate { get; set; }

    [Display(Name = "Ngày kết thúc thuê"), DataType(DataType.Date)]
    public DateOnly? RentalEndDate { get; set; }

    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (WarehouseType != "RENTED") yield break;
        if (!RentalStartDate.HasValue)
            yield return new ValidationResult("Kho thuê phải có ngày bắt đầu thuê.", [nameof(RentalStartDate)]);
        if (!RentalEndDate.HasValue)
            yield return new ValidationResult("Kho thuê phải có ngày kết thúc thuê.", [nameof(RentalEndDate)]);
        if (RentalStartDate.HasValue && RentalEndDate.HasValue && RentalEndDate < RentalStartDate)
            yield return new ValidationResult("Ngày kết thúc thuê không được trước ngày bắt đầu thuê.", [nameof(RentalEndDate)]);
    }
}
