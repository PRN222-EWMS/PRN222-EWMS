using System.ComponentModel.DataAnnotations;

namespace PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;

public class ReturnOrderFormModel
{
    public long? ReturnOrderId { get; set; }
    public string? ReturnCode { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn đơn bán hàng."), Display(Name = "Đơn bán hàng")]
    public long SalesOrderId { get; set; }

    public string? SalesOrderCode { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn kho tiếp nhận."), Display(Name = "Kho tiếp nhận")]
    public int WarehouseId { get; set; }

    [StringLength(1000, ErrorMessage = "Lý do trả hàng không được vượt quá 1000 ký tự."), Display(Name = "Lý do trả hàng")]
    public string? Reason { get; set; }

    public byte[]? RowVersion { get; set; }

    public string Action { get; set; } = "SaveDraft"; // "SaveDraft" or "SaveAndConfirm"

    public string? CustomerName { get; set; }
    public string? Status { get; set; }

    public List<EligibleSalesOrderOptionDto> EligibleSalesOrders { get; set; } = [];
    public List<WarehouseOptionDto> ActiveWarehouses { get; set; } = [];
    public List<EligibleSalesOrderLineDto> AvailableLines { get; set; } = [];

    public List<ReturnOrderLineItemModel> Lines { get; set; } = [];
}

public class ReturnOrderLineItemModel
{
    public long? ReturnLineId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn mặt hàng cần trả.")]
    public long SolineId { get; set; }

    public int ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; }

    public int ShippedQuantity { get; set; }
    public int EligibleQuantity { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng yêu cầu phải lớn hơn 0."), Display(Name = "Số lượng yêu cầu")]
    public int RequestedQuantity { get; set; }
}

