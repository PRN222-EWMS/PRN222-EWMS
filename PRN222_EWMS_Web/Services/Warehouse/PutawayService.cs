using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.WarehouseStaff;
using PRN222_EWMS_Web.ViewModels.Putaway;
using PRN222_EWMS_Web.Services.Purchasing;

namespace PRN222_EWMS_Web.Services.WarehouseStaff;

public class PutawayService(IPutawayRepository repo, IWarehouseCapacityEngine capacity) : IPutawayService
{
    private async Task<PutawayActor> ActorAsync(int userId, CancellationToken ct) =>
        await repo.GetActorAsync(userId, ct) ?? throw new PutawayException("Tài khoản chưa được phân công kho hoặc không còn quyền nhân viên kho.", 403);

    public async Task<PutawayIndexViewModel> GetIndexAsync(int userId, PutawayFilter filter, CancellationToken ct)
    {
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        filter.Condition = string.IsNullOrWhiteSpace(filter.Condition) ? null : filter.Condition;
        Validate(filter);
        var actor = await ActorAsync(userId, ct);
        return new PutawayIndexViewModel { WarehouseName = actor.WarehouseName, Queue = await repo.GetQueueAsync(actor.WarehouseId, filter, ct) };
    }

    public async Task<PutawaySerialViewModel> LookupAsync(int userId, string serial, CancellationToken ct)
    {
        serial = serial?.Trim() ?? "";
        if (serial.Length is < 1 or > 100 || serial.Any(char.IsControl)) throw new PutawayException("Nhập hoặc quét Serial dài 1–100 ký tự.", 400);
        var actor = await ActorAsync(userId, ct);
        var item = await repo.GetSerialAsync(actor.WarehouseId, null, serial, false, ct)
            ?? throw new PutawayException("Không tìm thấy Serial trong kho được giao. Kiểm tra đúng mã trên tem.", 404);
        var layout = await repo.GetLayoutAsync(actor.WarehouseId, ct) ?? throw new PutawayException("Kho được giao không còn tồn tại.", 404);
        var model = ToSerialModel(item, layout);
        model.UnavailableReason = PutawayRules.SerialUnavailable(item, layout, await repo.HasPickingClaimAsync(item.SerialItemId, ct));
        if (model.UnavailableReason is not null) return model;
        var occupants = await repo.GetOccupantsAsync(actor.WarehouseId, null, ct);
        var evaluations = layout.Locations.Where(l => l.LocationType is "TIER" or "FLOOR_SLOT")
            .Select(l => PutawayRules.Evaluate(item, layout, l, occupants)).ToList();
        model.Locations = evaluations.Where(e => e.Location is not null).Select(e => e.Location!)
            .OrderBy(l => l.IsReserve).ThenBy(l => l.Path, StringComparer.Ordinal).ThenBy(l => l.Id).ToList();
        var planned = await capacity.AssessPlacementsAsync(actor.WarehouseId, item.SerialItemId,
            model.Locations.Select(l => l.Id).ToArray(), ct);
        var plannedErrors = planned.Errors;
        model.CapacityWarnings = planned.Warnings;
        model.Locations = model.Locations.Where(l => !plannedErrors.ContainsKey(l.Id)).ToList();
        if (model.Locations.Count == 0)
            model.EmptyReason = plannedErrors.Count > 0 ? plannedErrors.Values.First()
                : evaluations.Count == 0 ? "Kho chưa có tầng kệ hoặc ô sàn được cấu hình."
                : "Không có vị trí phù hợp: " + string.Join(" ", evaluations.Select(e => e.Reason).Where(r => r is not null).Distinct().Take(4));
        return model;
    }

    public async Task<PutawayResult> ConfirmAsync(int userId, ConfirmPutawayInput input, CancellationToken ct)
    {
        Validate(input);
        var version = new byte[8];
        if (!Convert.TryFromBase64String(input.RowVersion, version, out var written) || written != 8)
            throw new PutawayException("Phiên bản Serial không hợp lệ. Hãy tra cứu lại.", 400);
        var actor = await ActorAsync(userId, ct);
        return await repo.InTransactionAsync(userId, actor.WarehouseId, async token =>
        {
            var currentActor = await ActorAsync(userId, token);
            if (currentActor.WarehouseId != actor.WarehouseId) throw new PutawayException("Phân công kho đã thay đổi. Hãy tải lại trang.", 403);
            await repo.LockSerialAsync(actor.WarehouseId, input.SerialItemId, token);
            var item = await repo.GetSerialAsync(actor.WarehouseId, input.SerialItemId, null, true, token)
                ?? throw new PutawayException("Serial không còn trong kho được giao.", 404);
            var layout = await repo.GetLayoutAsync(actor.WarehouseId, token) ?? throw new PutawayException("Không tìm thấy kho.", 404);
            var reason = PutawayRules.SerialUnavailable(item, layout, await repo.HasPickingClaimAsync(item.SerialItemId, token));
            if (reason is not null)
                throw new PutawayException(item.CurrentLocationId is int current ? $"Serial đã được xếp tại {PutawayRules.Path(layout, current)}. Hãy tra cứu lại để xem trạng thái hiện tại." : reason);
            if (!item.RowVersion.SequenceEqual(version)) throw new PutawayException("Serial đã thay đổi. Hãy tải lại gợi ý trước khi xác nhận.");
            var target = layout.Locations.SingleOrDefault(l => l.LocationId == input.TargetLocationId)
                ?? throw new PutawayException("Vị trí đích không tồn tại trong kho được giao.", 404);
            var evaluation = PutawayRules.Evaluate(item, layout, target, await repo.GetOccupantsAsync(actor.WarehouseId, target.LocationId, token));
            if (evaluation.Location is null) throw new PutawayException(evaluation.Reason ?? "Vị trí không còn hợp lệ.");
            var plannedErrors = await capacity.GetPlacementErrorsAsync(actor.WarehouseId, item.SerialItemId, [target.LocationId], token);
            if (plannedErrors.TryGetValue(target.LocationId, out var plannedReason)) throw new PutawayException(plannedReason);
            await repo.SavePlacementAsync(item, target.LocationId, userId, version, evaluation.Location.Path, token);
            var receipt = ReceiptInfo(item.CurrentSupplySource);
            return new PutawayResult(item.SerialNumber, evaluation.Location.Path, receipt.Source, receipt.ReceiptId);
        }, ct);
    }

    private static PutawaySerialViewModel ToSerialModel(SerialItem item, PutawayLayout layout)
    {
        var source = item.CurrentSupplySource;
        var receipt = ReceiptInfo(source);
        var transfer = source?.SourceType == "TRANSFER_RECEIPT" ? source.TransferReceipt?.TransferOrder : null;
        return new PutawaySerialViewModel
        {
            Id = item.SerialItemId, Serial = item.SerialNumber, ProductCode = item.Product.ProductCode,
            ProductName = item.Product.ProductName, Category = item.Product.Category.CategoryName, Brand = item.Product.Brand.BrandName,
            WarehouseName = layout.Warehouse.WarehouseName, Source = receipt.Source,
            ReceiptId = receipt.ReceiptId, ReceiptCode = receipt.ReceiptCode ?? "Chưa có nguồn nhận hợp lệ",
            TransferOrderId = transfer?.TransferOrderId, TransferCode = transfer?.TransferCode,
            FromWarehouseName = transfer?.FromWarehouse?.WarehouseName,
            Condition = item.ItemCondition,
            LengthCm = item.Product.LengthCm, WidthCm = item.Product.WidthCm, HeightCm = item.Product.HeightCm, WeightKg = item.Product.GrossWeightKg,
            RowVersion = Convert.ToBase64String(item.RowVersion),
            CurrentLocationPath = item.CurrentLocationId is int id ? PutawayRules.Path(layout, id) : null
        };
    }

    private static (string Source, long? ReceiptId, string? ReceiptCode) ReceiptInfo(SupplySource? source) => source?.SourceType switch
    {
        "INBOUND" => ("PURCHASE", source.InboundId, source.Inbound?.ReceiptCode),
        "RETURN" => ("RETURN", source.ReturnReceiptId, source.ReturnReceipt?.ReceiptCode),
        "TRANSFER_RECEIPT" => ("TRANSFER_RECEIPT", source.TransferReceiptId, source.TransferReceipt?.ReceiptCode),
        _ => ("PURCHASE", null, null)
    };

    private static void Validate(object input)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
            throw new PutawayException(errors.FirstOrDefault()?.ErrorMessage ?? "Dữ liệu không hợp lệ.", 400);
    }
}
