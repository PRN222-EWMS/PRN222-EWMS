using PRN222_EWMS_Web.Services.Purchasing;
using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class TransferCapacityService(IWarehouseCapacityEngine capacity)
{
    public async Task EvaluateAsync(CreateTransferOrderViewModel model, TransferAssessment result, long? excludeOrderId = null)
    {
        var check = await capacity.EvaluateAsync(new()
        {
            WarehouseId = model.ToWarehouseId, ExpectedArrivalDate = model.ExpectedDate,
            ExcludeTransferOrderId = excludeOrderId,
            Demands = model.Lines.Select(l => new CapacityDemand(l.ProductId, l.Quantity, model.ItemCondition)).ToList()
        });
        result.CurrentCapacity = check.PhysicalFits
            ? "Chỗ vật lý hiện tại: đủ cho lệnh và hàng đang chờ xếp."
            : "Chỗ vật lý hiện tại: chưa đủ hoặc kho không đủ điều kiện nhận hàng.";
        result.ForecastCapacity = check.IsValid
            ? "Sau khi dự phòng hàng mua, hàng chuyển và khách trả chưa nhận: đủ chỗ."
            : "Sau khi dự phòng các đợt nhận đã xác nhận: " + string.Join(" ", check.Errors);
        result.Warnings.AddRange(check.Warnings);
        if (!check.IsValid) result.Errors.Add(result.ForecastCapacity);
    }
}
