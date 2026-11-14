using PRN222_EWMS_Web.Repositories.Ceo;
using PRN222_EWMS_Web.ViewModels.Ceo;

namespace PRN222_EWMS_Web.Services.Ceo;

public class CeoDashboardService(ICeoDashboardRepository repository, TimeProvider clock) : ICeoDashboardService
{
    public async Task<CeoDashboardViewModel> GetDashboardAsync(CeoDashboardFilter filter)
    {
        var now = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        var today = DateOnly.FromDateTime(now.DateTime);
        if (filter.Days is < 7 or > 90 || filter.WarehouseId is <= 0)
            return new CeoDashboardViewModel { Filter = filter, Error = "Vui lòng chọn kho và khoảng theo dõi hợp lệ." };

        var start = today.AddDays(1 - filter.Days);
        var fromUtc = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), now.Offset).UtcDateTime;
        var toUtc = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), now.Offset).UtcDateTime;
        var model = await repository.GetDashboardAsync(filter, fromUtc, toUtc, today);
        model.PeriodStart = start;
        model.PeriodEnd = today;
        model.UpdatedAt = now.DateTime;
        if (model.Error is not null) return model;

        var recorded = model.Trend.ToDictionary(day => day.Day);
        model.Trend = Enumerable.Range(0, filter.Days).Select(offset =>
        {
            var day = start.AddDays(offset);
            return recorded.GetValueOrDefault(day) ?? new CeoDashboardDay { Day = day };
        }).ToList();

        foreach (var warehouse in model.Warehouses)
        {
            if (!warehouse.IsOperational)
                model.Attention.Add(new("danger", "Kho chưa đủ điều kiện hoạt động",
                    $"{warehouse.Name}: ngừng hoạt động hoặc ngoài kỳ thuê; còn {warehouse.InventoryCount:N0} serial trong kho.", warehouse.Id));
            else if (warehouse.LeaseDaysLeft is >= 0 and <= 30)
                model.Attention.Add(new("warning", "Kho sắp hết hạn thuê",
                    $"{warehouse.Name}: còn {warehouse.LeaseDaysLeft} ngày thuê, đến {warehouse.RentalEndDate:dd/MM/yyyy}.", warehouse.Id));

            if (warehouse.OverloadedCount > 0)
                model.Attention.Add(new("danger", "Vị trí đang quá tải",
                    $"{warehouse.Name}: {warehouse.OverloadedCount:N0} vị trí vượt diện tích, thể tích hoặc tải trọng đã cấu hình.", warehouse.Id));
            if (warehouse.InvalidLocationCount > 0)
                model.Attention.Add(new("warning", "Cần kiểm tra cấu hình vị trí",
                    $"{warehouse.Name}: {warehouse.InvalidLocationCount:N0} vị trí có cấu hình hoặc hồ sơ hàng chưa hợp lệ; số liệu sức chứa có thể chưa đầy đủ.", warehouse.Id));
            if (warehouse.NearFullCount > 0)
                model.Attention.Add(new("warning", "Vị trí gần đầy",
                    $"{warehouse.Name}: {warehouse.NearFullCount:N0} vị trí đã dùng từ 80% sức chứa hoặc tải trọng.", warehouse.Id));
            if (warehouse.DamagedCount > 0)
                model.Attention.Add(new("warning", "Hàng hỏng đang lưu kho",
                    $"{warehouse.Name}: {warehouse.DamagedCount:N0} serial hỏng, trong đó hàng hỏng vẫn chiếm sức chứa.", warehouse.Id));
            if (warehouse.AwaitingCount > 0)
                model.Attention.Add(new("info", "Hàng đang chờ xếp",
                    $"{warehouse.Name}: {warehouse.AwaitingCount:N0} serial đã nhận chưa có vị trí.", warehouse.Id));
        }
        if (model.Workload.PendingApprovalCount > 0)
            model.Attention.Insert(0, new("warning", "Điều chỉnh kiểm kê chờ CEO duyệt",
                $"Có {model.Workload.PendingApprovalCount:N0} đề nghị giảm tồn chưa được quyết định."));
        return model;
    }
}
