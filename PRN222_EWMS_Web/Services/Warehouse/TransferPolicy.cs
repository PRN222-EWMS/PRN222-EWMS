using PRN222_EWMS_Web.ViewModels.Transfer;

namespace PRN222_EWMS_Web.Services.Warehouse;

public static class TransferPolicy
{
    public const string ExpiryReason = "Tự động hủy vì quá ngày dự kiến đến và chưa có phiếu gửi/nhận. Hãy tạo lệnh mới.";

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));

    public static bool IsExpired(string status, DateOnly? eta, DateOnly today, bool hasDocuments) =>
        status is "DRAFT" or "RELEASED" && eta.HasValue && eta.Value < today && !hasDocuments;

    public static List<string> Validate(CreateTransferOrderViewModel model, DateOnly today, bool release) =>
        ValidateWithFields(model, today, release).Errors;

    public static (List<string> Errors, Dictionary<string, string> FieldErrors) ValidateWithFields(
        CreateTransferOrderViewModel model, DateOnly today, bool release)
    {
        var errors = new List<string>();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        void AddFieldError(string key, string msg)
        {
            errors.Add(msg);
            fields.TryAdd(key, msg);
        }

        if (model.FromWarehouseId <= 0)
            AddFieldError(nameof(model.FromWarehouseId), "Vui lòng chọn kho nguồn.");
        if (model.ToWarehouseId <= 0)
            AddFieldError(nameof(model.ToWarehouseId), "Vui lòng chọn kho đích.");
        if (model.FromWarehouseId > 0 && model.ToWarehouseId > 0 && model.FromWarehouseId == model.ToWarehouseId)
            AddFieldError(nameof(model.ToWarehouseId), "Kho đích phải khác kho nguồn.");
        if (model.ItemCondition is not ("GOOD" or "DAMAGED"))
            AddFieldError(nameof(model.ItemCondition), "Tình trạng hàng hóa phải là GOOD hoặc DAMAGED.");
        if (model.Note?.Length > 1000)
            AddFieldError(nameof(model.Note), "Ghi chú tối đa 1.000 ký tự.");

        if (model.Lines is null || model.Lines.Count is < 1 or > 100)
        {
            AddFieldError(nameof(model.Lines), "Lệnh chuyển cần từ 1 đến 100 dòng sản phẩm.");
        }
        else
        {
            var seenProducts = new HashSet<int>();
            for (int i = 0; i < model.Lines.Count; i++)
            {
                var line = model.Lines[i];
                if (line.ProductId <= 0)
                    AddFieldError($"Lines[{i}].ProductId", "Vui lòng chọn sản phẩm.");
                else if (!seenProducts.Add(line.ProductId))
                    AddFieldError($"Lines[{i}].ProductId", "Sản phẩm bị trùng, vui lòng gộp số lượng vào một dòng.");

                if (line.Quantity is < 1 or > 1000000)
                    AddFieldError($"Lines[{i}].Quantity", "Số lượng phải là số nguyên từ 1 đến 1.000.000.");
            }
        }

        if (release && !model.DispatchDate.HasValue)
            AddFieldError(nameof(model.DispatchDate), "Vui lòng nhập ngày dự kiến gửi.");
        if (release && !model.ExpectedDate.HasValue)
            AddFieldError(nameof(model.ExpectedDate), "Vui lòng nhập ngày dự kiến đến.");
        if (model.ExpectedDate.HasValue && model.ExpectedDate.Value < today)
            AddFieldError(nameof(model.ExpectedDate), "Ngày dự kiến đến không được nhỏ hơn ngày hôm nay.");
        if (model.DispatchDate.HasValue && model.ExpectedDate.HasValue && model.DispatchDate.Value > model.ExpectedDate.Value)
            AddFieldError(nameof(model.ExpectedDate), "Ngày dự kiến đến phải từ ngày dự kiến gửi trở đi.");

        // A missed dispatch plan does not expire an order; ExpectedDate is the deadline.
        return (errors, fields);
    }

    public static long FreeQuantity(long eligible, long serials, long held, long claimed, long claimsCoveredByHolds) =>
        Math.Max(0, Math.Min(eligible, serials) - held - Math.Max(0, claimed - claimsCoveredByHolds));
}
