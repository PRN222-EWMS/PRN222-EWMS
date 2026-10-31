using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PRN222_EWMS_Web.ViewModels.Warehouse;

public class StorageLocationFormViewModel : IValidatableObject
{
    [BindNever] public int WarehouseId { get; set; }
    [BindNever] public int LocationId { get; set; }
    [BindNever] public string WarehouseName { get; set; } = "";
    [BindNever] public string? ParentName { get; set; }
    [BindNever] public string? ParentConditionLabel { get; set; }
    [BindNever] public bool HasChildren { get; set; }
    [BindNever] public bool IsOccupied { get; set; }
    [BindNever] public bool IsLocked { get; set; }
    [BindNever] public List<SelectListItem> Categories { get; set; } = [];
    [BindNever] public List<SelectListItem> Brands { get; set; } = [];
    public int? ParentLocationId { get; set; }
    public string LocationType { get; set; } = "ZONE";
    [Required(ErrorMessage = "Vui lòng nhập tên vị trí.")]
    [StringLength(100, ErrorMessage = "Tên vị trí không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên vị trí")]
    public string LocationName { get; set; } = "";
    [Display(Name = "Loại Zone")] public string? ZoneType { get; set; }
    [Display(Name = "Kiểu lưu trữ của dãy")] public string? StorageType { get; set; }
    [Display(Name = "Tình trạng hàng được phép")] public string? ConditionPolicy { get; set; }
    [Display(Name = "Trạng thái")]
    [Required, RegularExpression("ACTIVE|INACTIVE", ErrorMessage = "Trạng thái không hợp lệ.")]
    public string Status { get; set; } = "ACTIVE";
    [Display(Name = "Chiều dài sử dụng (cm)")] public decimal? LengthCm { get; set; }
    [Display(Name = "Chiều rộng sử dụng (cm)")] public decimal? WidthCm { get; set; }
    [Display(Name = "Chiều cao sử dụng / giới hạn kiện (cm)")] public decimal? HeightCm { get; set; }
    [Display(Name = "Tải trọng tối đa (kg)")] public decimal? MaxWeightKg { get; set; }
    [StringLength(32)] public string? RowVersion { get; set; }
    public List<ZoneRuleFormViewModel> Rules { get; set; } = [];
    public string TypeLabel => LocationType switch
    {
        "ZONE" => "Zone", "ROW" => "Dãy", "RACK" => "Kệ", "TIER" => "Tầng", "FLOOR_SLOT" => "Ô sàn", _ => "Vị trí"
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LocationType is not ("ZONE" or "ROW" or "RACK" or "TIER" or "FLOOR_SLOT"))
            yield return new("Cấp vị trí không hợp lệ.", [nameof(LocationType)]);
        if (LocationType == "ZONE" && ZoneType is not ("STANDARD" or "RESERVE"))
            yield return new("Vui lòng chọn Zone tiêu chuẩn hoặc dự trữ.", [nameof(ZoneType)]);
        if (LocationType == "ROW" && StorageType is not ("RACK" or "FLOOR"))
            yield return new("Vui lòng chọn dãy để kệ hoặc để sàn.", [nameof(StorageType)]);
        if (LocationType is "RACK" or "FLOOR_SLOT" && ConditionPolicy is not ("GOOD" or "DAMAGED"))
            yield return new("Vui lòng chọn khu hàng tốt hoặc hàng hỏng.", [nameof(ConditionPolicy)]);
        if (LocationType == "ZONE" && ZoneType == "STANDARD" && (Rules.Count == 0 || Rules.Count > 100))
            yield return new("Zone tiêu chuẩn phải có từ 1 đến 100 cặp loại hàng và hãng.", [nameof(Rules)]);
        if (LocationType is not ("TIER" or "FLOOR_SLOT")) yield break;
        foreach (var (field, value, required) in new[] {
            (nameof(LengthCm), LengthCm, true), (nameof(WidthCm), WidthCm, true),
            (nameof(HeightCm), HeightCm, LocationType == "TIER") })
        {
            if (required && value is null) yield return new("Vui lòng nhập kích thước sử dụng.", [field]);
            if (value.HasValue && (value <= 0 || value > 99999999.99m || decimal.Round(value.Value, 2) != value.Value))
                yield return new("Kích thước phải dương, tối đa 99.999.999,99 cm và không quá 2 chữ số thập phân.", [field]);
        }
        if (MaxWeightKg.HasValue && (MaxWeightKg <= 0 || MaxWeightKg > 999999999.999m || decimal.Round(MaxWeightKg.Value, 3) != MaxWeightKg.Value))
            yield return new("Tải trọng phải dương, tối đa 999.999.999,999 kg và không quá 3 chữ số thập phân.", [nameof(MaxWeightKg)]);
    }
}

public class ZoneRuleFormViewModel
{
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
}
