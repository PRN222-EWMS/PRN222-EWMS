using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Manager;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Services.Manager;

public class ManagerStocktakeService(IManagerStocktakeRepository repository, IStocktakeRepository stocktakeHistory) : IManagerStocktakeService
{
    public Task<ManagerStocktakeListViewModel> GetListAsync(ManagerStocktakeFilter filter, CancellationToken ct)
    {
        filter.Search = Clean(filter.Search);
        filter.Status = Clean(filter.Status)?.ToUpperInvariant();
        if (filter.Search?.Length > 100) throw new ManagerStocktakeException("Từ khóa tối đa 100 ký tự.", "Search");
        if (filter.Status is not null && !ManagerStocktakeLabels.Statuses.Contains(filter.Status))
            throw new ManagerStocktakeException("Trạng thái kiểm kê không hợp lệ.", "Status");
        if (filter.WarehouseId <= 0) throw new ManagerStocktakeException("Kho đã chọn không hợp lệ.", "WarehouseId");
        if (filter.FromDate > filter.ToDate) throw new ManagerStocktakeException("Ngày kết thúc phải từ ngày bắt đầu trở đi.", "ToDate");
        filter.Page = Math.Max(1, filter.Page);
        return repository.GetListAsync(filter, ct);
    }

    public async Task<ManagerStocktakeDetailViewModel?> GetDetailsAsync(long id, ManagerStocktakeDetailFilter filter, CancellationToken ct)
    {
        filter.Search = Clean(filter.Search);
        filter.DifferenceType = Clean(filter.DifferenceType)?.ToUpperInvariant();
        if (filter.Search?.Length > 100) throw new ManagerStocktakeException("Từ khóa tối đa 100 ký tự.");
        if (filter.DifferenceType is not null && !ManagerStocktakeLabels.Differences.Contains(filter.DifferenceType))
            throw new ManagerStocktakeException("Loại chênh lệch không hợp lệ.");
        if (filter.RoundNumber <= 0) throw new ManagerStocktakeException("Vòng đếm không hợp lệ.");
        filter.Page = Math.Max(1, filter.Page);
        ManagerStocktakeDetailViewModel? detail;
        try { detail = await repository.GetDetailsAsync(id, filter, ct); }
        catch (ArgumentException) { throw new ManagerStocktakeException("Vòng đếm đã chọn không thuộc đợt kiểm kê này."); }
        if (detail is null) return null;

        // Existing seed/legacy plans may not yet cover awaiting stock. Read them as-is.
        if (detail.CanEdit)
        {
            var data = await repository.GetPlanningDataAsync(detail.WarehouseId, ct);
            if (data is not null)
            {
                if (!data.Warehouse.CanPlan(ManagerStocktakeLabels.Today))
                    detail.Warnings.Add("Kho đang ngừng hoạt động hoặc ngoài thời hạn thuê. Không thể lưu kế hoạch; đợt chưa bắt đầu vẫn được xóa.");
                try
                {
                    PrepareLocations(data.Locations);
                    var scopes = await repository.GetScopesAsync(id, ct);
                    var assignments = await repository.GetAssignmentsAsync(id, ct);
                    var form = new ManagerStocktakeFormViewModel
                    {
                        ScopeType = detail.ScopeType, IncludeAwaitingPutaway = detail.IncludeAwaitingPutaway,
                        SaveMode = "PLANNED", ScopeLocationIds = scopes.Select(s => s.LocationId).ToList(),
                        Assignments = assignments.Where(a => a.Status != "CANCELLED").Select(a => new ManagerStocktakeAssignmentInput
                        { UserId = a.UserId, ScopeKind = a.ScopeKind, LocationId = a.LocationId }).ToList()
                    };
                    ValidateAssignments(form, data, NormalizeRoots(form.ScopeLocationIds, data.Locations));
                }
                catch (ManagerStocktakeException ex) { detail.Warnings.Add("Kế hoạch cần rà lại: " + ex.Message); }
            }
        }
        return detail;
    }

    public async Task<ManagerStocktakeFormViewModel> CreateFormAsync(int? warehouseId, CancellationToken ct)
    {
        var form = new ManagerStocktakeFormViewModel { WarehouseId = warehouseId ?? 0 };
        await PopulateFormAsync(form, ct);
        return form;
    }

    public async Task<ManagerStocktakeFormViewModel?> EditFormAsync(long id, CancellationToken ct)
    {
        var plan = await repository.FindAsync(id, false, ct);
        if (plan is null) return null;
        await EnsureUnstartedAsync(plan, ct);
        var scopes = await repository.GetScopesAsync(id, ct);
        var assignments = await repository.GetAssignmentsAsync(id, ct);
        var form = new ManagerStocktakeFormViewModel
        {
            Id = id, Code = plan.StocktakeCode, WarehouseId = plan.WarehouseId,
            ScopeType = plan.ScopeType, IncludeAwaitingPutaway = plan.IncludeAwaitingPutaway,
            PlannedAt = plan.PlannedAt.HasValue ? DateTime.SpecifyKind(ManagerStocktakeLabels.Local(plan.PlannedAt.Value), DateTimeKind.Unspecified) : null,
            Note = plan.Note, SaveMode = plan.Status, RowVersion = Convert.ToBase64String(plan.RowVersion),
            ScopeLocationIds = scopes.Select(s => s.LocationId).ToList(),
            Assignments = assignments.Where(a => a.Status != "CANCELLED").Select(a => new ManagerStocktakeAssignmentInput
            { UserId = a.UserId, ScopeKind = a.ScopeKind, LocationId = a.LocationId }).ToList()
        };
        await PopulateFormAsync(form, ct);
        return form;
    }

    public async Task PopulateFormAsync(ManagerStocktakeFormViewModel form, CancellationToken ct)
    {
        form.Warnings = [];
        if (form.Id.HasValue)
        {
            var existing = await repository.FindAsync(form.Id.Value, false, ct);
            if (existing is not null)
            {
                form.Code = existing.StocktakeCode;
                form.WarehouseId = existing.WarehouseId;
            }
        }
        form.Warehouses = (await repository.GetWarehousesAsync(ct))
            .Where(w => w.CanPlan(ManagerStocktakeLabels.Today) || (form.Id.HasValue && w.Id == form.WarehouseId)).ToList();
        form.Locations = [];
        form.Staff = [];
        form.AwaitingCount = 0;
        if (form.Warehouses.Count == 0) form.Warnings.Add("Chưa có kho đang hoạt động và còn thời hạn thuê để lập kế hoạch.");
        if (form.WarehouseId <= 0) return;
        var data = await repository.GetPlanningDataAsync(form.WarehouseId, ct);
        if (data is null) { form.Warnings.Add("Kho đã chọn không còn tồn tại."); return; }
        if (!data.Warehouse.CanPlan(ManagerStocktakeLabels.Today))
            form.Warnings.Add("Kho đang ngừng hoạt động hoặc ngoài thời hạn thuê. Không thể lưu kế hoạch; vẫn có thể xóa đợt chưa bắt đầu.");
        try { PrepareLocations(data.Locations); form.Locations = data.Locations.OrderBy(l => l.Path, StringComparer.CurrentCulture).ToList(); }
        catch (ManagerStocktakeException ex) { form.Warnings.Add(ex.Message); }
        form.Staff = data.Staff;
        form.AwaitingCount = data.AwaitingCount;
        if (form.Staff.Count == 0) form.Warnings.Add("Kho chưa có Warehouse Staff đang hoạt động. Có thể lưu nháp, chưa thể hoàn tất kế hoạch.");
    }

    public async Task<long> SaveAsync(ManagerStocktakeFormViewModel form, int actorId, CancellationToken ct)
    {
        if (form.ScopeType is not ("WHOLE_WAREHOUSE" or "LOCATIONS"))
            throw new ManagerStocktakeException("Phạm vi kiểm kê không hợp lệ.", nameof(form.ScopeType));
        if (form.SaveMode is not ("DRAFT" or "PLANNED"))
            throw new ManagerStocktakeException("Chỉ được lưu nháp hoặc hoàn tất kế hoạch.", nameof(form.SaveMode));
        if (form.WarehouseId <= 0) throw new ManagerStocktakeException("Vui lòng chọn kho kiểm kê.", nameof(form.WarehouseId));
        if (form.Note?.Length > 1000) throw new ManagerStocktakeException("Ghi chú tối đa 1.000 ký tự.", nameof(form.Note));
        if (form.ScopeType == "WHOLE_WAREHOUSE") form.IncludeAwaitingPutaway = true;
        if (form.SaveMode == "PLANNED" && !form.PlannedAt.HasValue)
            throw new ManagerStocktakeException("Vui lòng chọn ngày, giờ dự kiến trước khi hoàn tất kế hoạch.", nameof(form.PlannedAt));
        var plannedDay = form.PlannedAt.HasValue ? DateOnly.FromDateTime(form.PlannedAt.Value) : ManagerStocktakeLabels.Today;
        if (form.PlannedAt.HasValue && plannedDay < ManagerStocktakeLabels.Today)
            throw new ManagerStocktakeException("Ngày dự kiến không được trước ngày hiện tại.", nameof(form.PlannedAt));

        try
        {
            return await repository.InTransactionAsync(actorId, async () =>
            {
                await EnsureManagerAsync(actorId, ct);
                Stocktake? plan = null;
                if (form.Id.HasValue)
                {
                    plan = await repository.FindAsync(form.Id.Value, true, ct)
                        ?? throw new ManagerStocktakeException("Đợt kiểm kê không còn tồn tại.", statusCode: 404);
                    await EnsureUnstartedAsync(plan, ct);
                    CheckVersion(form.RowVersion, plan.RowVersion);
                    if (form.WarehouseId != plan.WarehouseId)
                        throw new ManagerStocktakeException("Không được đổi kho của đợt đã tạo.", nameof(form.WarehouseId));
                }
                var data = await repository.GetPlanningDataAsync(form.WarehouseId, ct)
                    ?? throw new ManagerStocktakeException("Kho đã chọn không tồn tại.", nameof(form.WarehouseId));
                if (!data.Warehouse.CanPlan(ManagerStocktakeLabels.Today) || !data.Warehouse.CanPlan(plannedDay))
                    throw new ManagerStocktakeException("Kho phải đang hoạt động; hôm nay và ngày dự kiến đều phải nằm trong thời hạn thuê của kho.", nameof(form.WarehouseId));
                PrepareLocations(data.Locations);
                var roots = form.ScopeType == "WHOLE_WAREHOUSE" ? [] : NormalizeRoots(form.ScopeLocationIds, data.Locations);
                ValidateAssignments(form, data, roots);
                var rebuild = plan is null;
                if (plan is not null)
                {
                    var oldScopes = await repository.GetScopesAsync(plan.StocktakeId, ct);
                    var oldAssignments = await repository.GetAssignmentsAsync(plan.StocktakeId, ct);
                    rebuild = plan.ScopeType != form.ScopeType || plan.IncludeAwaitingPutaway != form.IncludeAwaitingPutaway
                        || !oldScopes.Select(s => s.LocationId).Order().SequenceEqual(roots.Order())
                        || !oldAssignments.Where(a => a.Status != "CANCELLED").Select(a => (a.UserId, a.ScopeKind, a.LocationId)).OrderBy(a => a.UserId).ThenBy(a => a.ScopeKind).ThenBy(a => a.LocationId)
                            .SequenceEqual(form.Assignments.Select(a => (a.UserId, a.ScopeKind, a.LocationId)).OrderBy(a => a.UserId).ThenBy(a => a.ScopeKind).ThenBy(a => a.LocationId));
                    // Scope deletion must happen while the old header is still DRAFT/PLANNED.
                    if (rebuild) await repository.ClearPlanChildrenAsync(plan.StocktakeId, ct);
                }
                else
                {
                    plan = new Stocktake
                    {
                        WarehouseId = form.WarehouseId, CreatedByUserId = actorId, CreatedAt = DateTime.UtcNow,
                        StocktakeCode = $"ST-{DateTime.UtcNow.AddHours(7):yyyyMMdd}-{Guid.NewGuid().ToString("N")[..20].ToUpperInvariant()}"
                    };
                }
                plan.ScopeType = form.ScopeType;
                plan.IncludeAwaitingPutaway = form.IncludeAwaitingPutaway;
                plan.Status = form.SaveMode;
                plan.PlannedAt = form.PlannedAt.HasValue ? ManagerStocktakeLabels.Utc(form.PlannedAt.Value) : null;
                plan.Note = Clean(form.Note);
                if (plan.StocktakeId == 0) await repository.InsertHeaderAsync(plan, ct);
                else await repository.SaveHeaderAsync(plan, ct);
                if (rebuild) await repository.InsertPlanChildrenAsync(plan, roots, form.Assignments, ct);
                return plan.StocktakeId;
            }, ct);
        }
        catch (DbUpdateConcurrencyException) { throw new ManagerStocktakeException("Kế hoạch vừa được người khác cập nhật. Vui lòng tải lại trang trước khi lưu.", statusCode: 409); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        { throw new ManagerStocktakeException("Mã đợt hoặc phân công bị trùng. Vui lòng tải lại và kiểm tra các vùng được giao."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 1205 or 1222)
        { throw new ManagerStocktakeException("Dữ liệu đang được thao tác đồng thời. Vui lòng tải lại và thực hiện lại.", statusCode: 409); }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        { throw new ManagerStocktakeException("Dữ liệu đang được thao tác đồng thời. Vui lòng tải lại và thực hiện lại.", statusCode: 409); }
    }

    public async Task DeleteAsync(ManagerStocktakeDeleteInput input, int actorId, CancellationToken ct)
    {
        try
        {
            await repository.InTransactionAsync(actorId, async () =>
            {
                await EnsureManagerAsync(actorId, ct);
                var plan = await repository.FindAsync(input.Id, true, ct)
                    ?? throw new ManagerStocktakeException("Đợt kiểm kê không còn tồn tại.", statusCode: 404);
                await EnsureUnstartedAsync(plan, ct);
                CheckVersion(input.RowVersion, plan.RowVersion);
                if ((await stocktakeHistory.InvestigationsAsync(plan.StocktakeId, ct)).Count > 0)
                    throw new ManagerStocktakeException("Đợt có liên kết đối soát. Cần giữ lại để tra cứu lịch sử.");
                await repository.ClearPlanChildrenAsync(plan.StocktakeId, ct);
                await repository.DeleteHeaderAsync(plan, ct);
                return true;
            }, ct);
        }
        catch (DbUpdateConcurrencyException) { throw new ManagerStocktakeException("Kế hoạch vừa thay đổi. Vui lòng xem lại trước khi xóa.", statusCode: 409); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 1205 or 1222)
        { throw new ManagerStocktakeException("Đợt kiểm kê đang được thao tác đồng thời. Vui lòng tải lại trang.", statusCode: 409); }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        { throw new ManagerStocktakeException("Đợt kiểm kê đang được thao tác đồng thời. Vui lòng tải lại trang.", statusCode: 409); }
    }

    private async Task EnsureManagerAsync(int actorId, CancellationToken ct)
    {
        if (!await repository.IsActiveManagerAsync(actorId, ct))
            throw new ManagerStocktakeException("Tài khoản Manager không còn hoạt động hoặc không có quyền thực hiện.", statusCode: 403);
    }

    private async Task EnsureUnstartedAsync(Stocktake plan, CancellationToken ct)
    {
        if (plan.Status is not ("DRAFT" or "PLANNED") || plan.StartedAt.HasValue
            || await repository.HasExecutionDataAsync(plan.StocktakeId, ct))
            throw new ManagerStocktakeException("Chỉ được sửa hoặc xóa đợt chưa bắt đầu và chưa phát sinh dữ liệu kiểm kê, điều chỉnh.", statusCode: 409);
    }

    private static void CheckVersion(string? version, byte[] current)
    {
        byte[] submitted;
        try { submitted = Convert.FromBase64String(version ?? ""); }
        catch (FormatException) { throw new ManagerStocktakeException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.", statusCode: 409); }
        if (submitted.Length != 8 || !submitted.SequenceEqual(current))
            throw new ManagerStocktakeException("Kế hoạch vừa thay đổi. Vui lòng tải lại trang trước khi tiếp tục.", statusCode: 409);
    }

    internal static void PrepareLocations(List<ManagerStocktakeLocationOption> locations)
    {
        var map = locations.ToDictionary(l => l.Id);
        foreach (var location in locations)
        {
            var names = new List<string>();
            var visited = new HashSet<int>();
            var node = location;
            while (true)
            {
                if (!visited.Add(node.Id)) throw new ManagerStocktakeException("Cấu trúc vị trí đang có vòng lặp. Cần sửa cấu trúc trước khi lập kế hoạch.");
                names.Add(node.Name);
                if (!node.ParentId.HasValue) break;
                if (!map.TryGetValue(node.ParentId.Value, out var parent))
                    throw new ManagerStocktakeException("Cấu trúc vị trí có nhánh cha không hợp lệ. Cần kiểm tra lại kho.");
                node = parent;
            }
            names.Reverse();
            location.Path = string.Join(" → ", names);
            location.Depth = names.Count - 1;
        }
    }

    private static List<int> NormalizeRoots(IEnumerable<int> ids, List<ManagerStocktakeLocationOption> locations)
    {
        var map = locations.ToDictionary(l => l.Id);
        var selected = ids.ToHashSet();
        if (selected.Any(id => !map.ContainsKey(id)))
            throw new ManagerStocktakeException("Có vị trí không thuộc kho đã chọn hoặc đã bị xóa.", "ScopeLocationIds");
        return selected.Where(id => !selected.Any(other => other != id && IsWithin(id, other, map))).Order().ToList();
    }

    private static bool IsWithin(int id, int root, IReadOnlyDictionary<int, ManagerStocktakeLocationOption> locations)
    {
        int? current = id;
        var visited = new HashSet<int>();
        while (current.HasValue && visited.Add(current.Value) && locations.TryGetValue(current.Value, out var node))
        {
            if (current.Value == root) return true;
            current = node.ParentId;
        }
        return false;
    }

    private static void ValidateAssignments(ManagerStocktakeFormViewModel form, ManagerStocktakePlanningData data, List<int> roots)
    {
        var complete = form.SaveMode == "PLANNED";
        if (form.ScopeType == "LOCATIONS" && roots.Count == 0 && (complete || form.Assignments.Count > 0))
            throw new ManagerStocktakeException("Vui lòng chọn ít nhất một vị trí trong phạm vi kiểm kê.", "ScopeLocationIds");
        var map = data.Locations.ToDictionary(l => l.Id);
        var parentIds = data.Locations.Where(l => l.ParentId.HasValue).Select(l => l.ParentId!.Value).ToHashSet();
        var leaves = data.Locations.Where(l => !parentIds.Contains(l.Id)).Select(l => l.Id).ToHashSet();
        var scopeLeaves = leaves.Where(id => form.ScopeType == "WHOLE_WAREHOUSE" || roots.Any(root => IsWithin(id, root, map))).ToHashSet();
        var staff = data.Staff.Select(s => s.Id).ToHashSet();
        var covered = new HashSet<int>();
        var awaitingCovered = false;
        for (var index = 0; index < form.Assignments.Count; index++)
        {
            var assignment = form.Assignments[index];
            var field = $"Assignments[{index}]";
            if (!staff.Contains(assignment.UserId))
                throw new ManagerStocktakeException($"Phân công {index + 1}: phải chọn Warehouse Staff đang hoạt động và thuộc đúng kho.", field + ".UserId");
            if (assignment.ScopeKind == "ALL_SCOPE")
            {
                if (form.Assignments.Count != 1 || assignment.LocationId.HasValue)
                    throw new ManagerStocktakeException("Phân công toàn phạm vi phải đứng riêng và không chọn vị trí.", field + ".ScopeKind");
                covered.UnionWith(scopeLeaves);
                awaitingCovered = form.IncludeAwaitingPutaway;
            }
            else if (assignment.ScopeKind == "AWAITING_PUTAWAY")
            {
                if (!form.IncludeAwaitingPutaway || assignment.LocationId.HasValue || awaitingCovered)
                    throw new ManagerStocktakeException("Hàng chờ xếp phải thuộc phạm vi đợt, chỉ giao một lần và không chọn vị trí.", field + ".ScopeKind");
                awaitingCovered = true;
            }
            else if (assignment.ScopeKind == "LOCATION")
            {
                if (!assignment.LocationId.HasValue || !map.ContainsKey(assignment.LocationId.Value)
                    || (form.ScopeType == "LOCATIONS" && !roots.Any(root => IsWithin(assignment.LocationId.Value, root, map))))
                    throw new ManagerStocktakeException($"Phân công {index + 1}: vị trí phải nằm trong phạm vi đã chọn của kho.", field + ".LocationId");
                var assignedLeaves = scopeLeaves.Where(id => IsWithin(id, assignment.LocationId.Value, map)).ToHashSet();
                if (assignedLeaves.Overlaps(covered))
                    throw new ManagerStocktakeException($"Phân công {index + 1} chồng lên vùng đã giao. Không giao đồng thời vị trí cha và vị trí con.", field + ".LocationId");
                covered.UnionWith(assignedLeaves);
            }
            else throw new ManagerStocktakeException("Loại phạm vi phân công không hợp lệ.", field + ".ScopeKind");
        }
        if (!complete) return;
        if (form.Assignments.Count == 0) throw new ManagerStocktakeException("Cần phân công ít nhất một nhân viên để hoàn tất kế hoạch.", "Assignments");
        if (!covered.SetEquals(scopeLeaves))
            throw new ManagerStocktakeException($"Còn {scopeLeaves.Except(covered).Count()} vị trí cuối cây chưa được giao. Hãy phân công phủ đủ phạm vi.", "Assignments");
        if (form.IncludeAwaitingPutaway && !awaitingCovered)
            throw new ManagerStocktakeException("Chưa phân công người kiểm hàng chờ xếp. Chọn người nhận phần này hoặc giao một người toàn phạm vi.", "Assignments");
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
