using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public static class TransferPolicy
{
    public const string ExpiryReason = "AUTO_EXPIRED_ETA: Tự động hủy vì quá ngày dự kiến đến và chưa có phiếu gửi/nhận. Hãy tạo lệnh mới.";

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));

    public static bool IsExpired(string status, DateOnly? eta, DateOnly today, bool hasDocuments) =>
        status is "DRAFT" or "RELEASED" && eta.HasValue && eta.Value < today && !hasDocuments;

    public static List<string> Validate(CreateTransferOrderViewModel model, DateOnly today, bool release)
    {
        var errors = new List<string>();
        if (model.FromWarehouseId <= 0 || model.ToWarehouseId <= 0) errors.Add("Hãy chọn đầy đủ kho nguồn và kho đích.");
        if (model.FromWarehouseId == model.ToWarehouseId) errors.Add("Kho nguồn và kho đích phải khác nhau.");
        if (model.ItemCondition is not ("GOOD" or "DAMAGED")) errors.Add("Tình trạng hàng phải là GOOD hoặc DAMAGED.");
        if (model.Note?.Length > 1000) errors.Add("Ghi chú tối đa 1.000 ký tự.");
        if (model.Lines is null || model.Lines.Count is < 1 or > 100) errors.Add("Lệnh cần từ 1 đến 100 dòng sản phẩm.");
        else
        {
            if (model.Lines.Any(l => l.ProductId <= 0 || l.Quantity is < 1 or > 1000000))
                errors.Add("Mỗi dòng cần một sản phẩm và số lượng nguyên từ 1 đến 1.000.000.");
            if (model.Lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
                errors.Add("Sản phẩm bị trùng dòng. Hãy gộp số lượng vào một dòng.");
        }
        if (release && (!model.DispatchDate.HasValue || !model.ExpectedDate.HasValue))
            errors.Add("Phát hành cần cả ngày dự kiến gửi và ngày dự kiến đến.");
        if (model.ExpectedDate < today) errors.Add("Ngày dự kiến đến đã quá hạn. Hãy tạo lệnh mới với lịch hợp lệ.");
        if (model.DispatchDate > model.ExpectedDate) errors.Add("Ngày dự kiến đến phải từ ngày dự kiến gửi trở đi.");
        // A missed dispatch plan does not expire an order; ETA is the deadline.
        return errors;
    }

    public static long FreeQuantity(long eligible, long serials, long held, long claimed, long claimsCoveredByHolds) =>
        Math.Max(0, Math.Min(eligible, serials) - held - Math.Max(0, claimed - claimsCoveredByHolds));
}
