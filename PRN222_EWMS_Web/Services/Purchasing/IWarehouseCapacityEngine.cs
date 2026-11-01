namespace PRN222_EWMS_Web.Services.Purchasing;

/// <summary>
/// Kết quả tính toán sức chứa cho một sản phẩm tại kho cụ thể theo công thức chuẩn WMS.
/// </summary>
public class ProductCapacityResult
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Kho có Zone hợp lệ (STANDARD khớp Category/Brand hoặc RESERVE) để chứa sản phẩm này không.
    /// Nếu false -> Lỗi MSG-015.
    /// </summary>
    public bool HasValidZone { get; set; }

    /// <summary>
    /// Sức chứa Ô sàn (FLOOR_SLOT) theo diện tích chân đế (m²).
    /// </summary>
    public int FloorSlotCapacity { get; set; }

    /// <summary>
    /// Sức chứa Dàn kệ (TIER) theo min(thể tích m³, tải trọng kg).
    /// </summary>
    public int RackTierCapacity { get; set; }

    /// <summary>
    /// Tổng sức chứa vật lý khả dụng hiện tại (Floor + Rack).
    /// </summary>
    public int TotalPhysicalCapacity => FloorSlotCapacity + RackTierCapacity;

    /// <summary>
    /// Nguồn hàng dự kiến sắp về (SupplySource INCOMING/IN_TRANSIT) của PO khác, trả hàng, điều chuyển.
    /// </summary>
    public int SupplyPipelineQuantity { get; set; }

    /// <summary>
    /// Hàng đã nhận tại bãi đệm đang chờ xếp kệ (SerialItem RECEIVED/IN_WAREHOUSE có CurrentLocationId == null).
    /// </summary>
    public int AwaitingPutawayQuantity { get; set; }

    /// <summary>
    /// Tổng lượng hàng đang trên đường hoặc chờ xếp vị trí.
    /// </summary>
    public int PipelineQuantity => SupplyPipelineQuantity + AwaitingPutawayQuantity;

    /// <summary>
    /// Sức chứa tối đa còn lại cho đơn đặt mới = max(0, TotalPhysicalCapacity - PipelineQuantity).
    /// </summary>
    public int MaxOrderQuantity => Math.Max(0, TotalPhysicalCapacity - PipelineQuantity);

    /// <summary>
    /// Đạt điều kiện sức chứa (Số lượng đặt <= MaxOrderQuantity và HasValidZone == true).
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Thông báo lỗi chi tiết theo chuẩn SRS (MSG-015 hoặc MSG-016) nếu không hợp lệ.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

public interface IWarehouseCapacityEngine
{
    /// <summary>
    /// Tính toán sức chứa chi tiết cho một sản phẩm tại kho chỉ định.
    /// </summary>
    /// <param name="warehouseId">ID kho tiếp nhận</param>
    /// <param name="productId">ID sản phẩm</param>
    /// <param name="requestedQuantity">Số lượng đặt mua muốn kiểm tra</param>
    /// <param name="excludePoId">ID PO đang chỉnh sửa (để loại trừ SupplySource cũ của chính đơn này khỏi Pipeline)</param>
    Task<ProductCapacityResult> CalculateCapacityAsync(
        int warehouseId,
        int productId,
        int requestedQuantity,
        long? excludePoId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate danh sách các dòng sản phẩm của PO theo công thức sức chứa chuẩn.
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidatePurchaseOrderLinesCapacityAsync(
        int warehouseId,
        IEnumerable<(int ProductId, int Quantity)> lines,
        long? excludePoId = null,
        CancellationToken cancellationToken = default);
}
