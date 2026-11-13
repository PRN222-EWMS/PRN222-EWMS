namespace PRN222_EWMS_Web.Services.Purchasing;

public sealed record CapacityDemand(int ProductId, long Quantity, string Condition = "GOOD");

public sealed class PlacementCapacityAssessment
{
    public Dictionary<int, string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
}

public sealed class WarehouseCapacityRequest
{
    public int WarehouseId { get; init; }
    public IReadOnlyList<CapacityDemand> Demands { get; init; } = [];
    public DateOnly? ExpectedArrivalDate { get; init; }
    public long? ExcludePoId { get; init; }
    public long? ExcludeTransferOrderId { get; init; }
    public long? ExcludeReturnOrderId { get; init; }
    public bool CalculateLimits { get; init; }
}

public sealed class WarehouseCapacityAssessment
{
    public bool IsValid => Errors.Count == 0;
    public bool PhysicalFits { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<ProductCapacityResult> Lines { get; } = [];
    public long IncomingPurchaseQuantity { get; set; }
    public long IncomingTransferQuantity { get; set; }
    public long IncomingReturnQuantity { get; set; }
    public long AwaitingPutawayQuantity { get; set; }
}

public class ProductCapacityResult
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public bool HasValidZone { get; set; }
    // Distribution of this line's net limit, after all other lines and commitments.
    public int FloorSlotCapacity { get; set; }
    public int RackTierCapacity { get; set; }
    public int TotalPhysicalCapacity => FloorSlotCapacity + RackTierCapacity;
    public int SupplyPipelineQuantity { get; set; }
    public int AwaitingPutawayQuantity { get; set; }
    public int PipelineQuantity => SupplyPipelineQuantity + AwaitingPutawayQuantity;
    public int MaxOrderQuantity { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IWarehouseCapacityEngine
{
    Task LockWarehouseAsync(int warehouseId, CancellationToken ct = default);
    Task<WarehouseCapacityAssessment> EvaluateAsync(WarehouseCapacityRequest request, CancellationToken ct = default);
    Task<ProductCapacityResult> CalculateCapacityAsync(int warehouseId, int productId, int requestedQuantity,
        long? excludePoId = null, CancellationToken cancellationToken = default);
    Task<(bool IsValid, string? ErrorMessage)> ValidatePurchaseOrderLinesCapacityAsync(int warehouseId,
        IEnumerable<(int ProductId, int Quantity)> lines, long? excludePoId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, string>> GetPlacementErrorsAsync(int warehouseId, long serialId,
        IReadOnlyCollection<int> locationIds, CancellationToken ct = default);
    Task<PlacementCapacityAssessment> AssessPlacementsAsync(int warehouseId, long serialId,
        IReadOnlyCollection<int> locationIds, CancellationToken ct = default);
    Task<PlacementCapacityAssessment> AssessStocktakePlacementsAsync(int warehouseId, long serialId, string condition,
        IReadOnlyCollection<int> locationIds, IReadOnlyCollection<long> stocktakeIds, CancellationToken ct = default);
}
