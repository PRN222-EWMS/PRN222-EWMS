using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;

namespace PRN222_EWMS_Web.ViewModels.Products;

public class ProductCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập mã sản phẩm.")]
    public string ProductCode { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
    public string ProductName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập model.")]
    public string Model { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập đơn vị.")]
    public string Unit { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thương hiệu.")]
    public int BrandId { get; set; }

    public string? Specifications { get; set; }

    [Range(typeof(decimal), "0.01", "999999",
        ErrorMessage = "Chiều dài phải từ 0.01 đến 999999 cm.")]
    public decimal LengthCm { get; set; }

    [Range(typeof(decimal), "0.01", "999999",
        ErrorMessage = "Chiều rộng phải từ 0.01 đến 999999 cm.")]
    public decimal WidthCm { get; set; }

    [Range(typeof(decimal), "0.01", "999999",
        ErrorMessage = "Chiều cao phải từ 0.01 đến 999999 cm.")]
    public decimal HeightCm { get; set; }

    [Range(typeof(decimal), "0.001", "999999",
        ErrorMessage = "Trọng lượng phải từ 0.001 đến 999999 kg.")]
    public decimal GrossWeightKg { get; set; }

    public List<SelectListItem> Categories { get; set; } = new();

    public List<SelectListItem> Brands { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(
    ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Specifications))
            yield break;

        bool isValidObject;

        try
        {
            using var document = JsonDocument.Parse(Specifications);

            isValidObject =
                document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            isValidObject = false;
        }

        if (!isValidObject)
        {
            yield return new ValidationResult(
                "Thông số kỹ thuật phải là JSON dạng đối tượng, " +
                "ví dụ: {\"CongNghe\":\"Inverter\"}.",
                new[] { nameof(Specifications) });
        }
    }
}