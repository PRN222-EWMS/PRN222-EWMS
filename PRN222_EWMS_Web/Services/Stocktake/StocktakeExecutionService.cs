using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Stocktaking;
using PRN222_EWMS_Web.ViewModels.Stocktaking;
using Plan = PRN222_EWMS_Web.Models.Stocktake;

namespace PRN222_EWMS_Web.Services.Stocktaking;

public class StocktakeExecutionService(IStocktakeRepository repo) : IStocktakeExecutionService
{
    private static readonly string[] LockedStatuses = ["COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL"];

    public async Task<StocktakeSerialLookup> LookupSerialAsync(int actorId, long id, long? recountRequestId, string serial, CancellationToken ct)
    {
        var task = await GetDetailsAsync(actorId, id, recountRequestId, ct);
        if (!task.CanCount) throw new StocktakeWorkflowException("Nhiệm vụ hiện không cho phép ghi nhận thêm.", 409);
        serial = serial?.Trim() ?? "";
        if (serial.Length is < 1 or > 100 || serial.Any(char.IsControl))
            throw new StocktakeWorkflowException("Serial phải dài 1–100 ký tự và không chứa ký tự điều khiển.");
        var item = await repo.Query<SerialItem>().AsNoTracking().Include(s => s.Product)
            .SingleOrDefaultAsync(s => s.SerialNumber == serial, ct);
        if (!StocktakeReceivingEvidence.IsInWarehouse(item, task.Plan.WarehouseId))
        {
            var receipts = await StocktakeReceivingEvidence.FindAsync(repo, serial, task.Plan.WarehouseId, "DRAFT", ct);
            return new(item is not null, item?.ProductId, item?.Product.ProductName, null, "", false,
                StocktakeReceivingEvidence.BlockedMessage(item, receipts));
        }
        if (item is not null && task.Requests.Any(r => r.ProductId != item.ProductId))
            throw new StocktakeWorkflowException("Serial không thuộc sản phẩm được yêu cầu đếm lại.");
        var round = task.RecountRequestId.HasValue
            ? task.Rounds.Single(r => r.RoundId == task.Requests.Single().ResultRoundId)
            : task.Rounds.Single(r => r.RoundNumber == 1);
        var observation = await repo.Query<StocktakeObservation>().AsNoTracking()
            .SingleOrDefaultAsync(o => o.StocktakeId == id && o.RoundNumber == round.RoundNumber && o.ScannedSerial == serial, ct);
        if (task.IsShortageSearch && observation?.SerialItemId != item?.SerialItemId)
            throw new StocktakeWorkflowException("Serial này không nằm trong danh sách cần tìm.", 403);
        if (recountRequestId is null && (await repo.HandlingAsync(id, ct)).Any(h => SameSerial(h.ScannedSerial, serial) && h.Status == "MOVED"))
            throw new StocktakeWorkflowException("Serial đã được xác nhận chuyển trong đợt này. Không đếm lại ở ô nhận hàng; xem lịch sử xử lý.", 409);
        if (observation?.ScannedAt is not null && observation.ScannedByUserId != actorId)
            throw new StocktakeWorkflowException("Serial đã được nhân viên khác ghi nhận trong vòng đếm này. Manager cần rà soát trước khi thay đổi.", 409);
        var expected = observation?.ExpectedQuantity == 1 ? observation.ExpectedLocationId : item?.CurrentLocationId;
        var parts = new List<string>();
        var seen = new HashSet<int>();
        var current = expected;
        while (current.HasValue && seen.Add(current.Value))
        {
            var node = task.Locations.FirstOrDefault(l => l.LocationId == current);
            if (node is null) break;
            parts.Insert(0, node.LocationName); current = node.ParentLocationId;
        }
        var label = expected.HasValue ? (parts.Count > 0 ? string.Join(" / ", parts) : "Vị trí ngoài kho được giao")
            : item?.CurrentWarehouseId == task.Plan.WarehouseId ? "Hàng chờ xếp kệ" : "Chưa có vị trí tại kho này";
        return new(item is not null, item?.ProductId, item?.Product.ProductName, expected, label, observation?.ScannedAt is not null);
    }

    private static bool SameSerial(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private async Task<User> ActorAsync(int actorId, CancellationToken ct) =>
        await repo.Query<User>().AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == actorId && u.Status == "ACTIVE"
                && u.Role.RoleName == "WAREHOUSE_STAFF" && u.WarehouseId != null, ct)
        ?? throw new StocktakeWorkflowException("Tài khoản không còn quyền nhân viên kho hoặc chưa được phân công kho.", 403);

    public async Task<StocktakeExecutionListViewModel> GetListAsync(int actorId, string? search, int page, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        search = search?.Trim();
        if (search?.Length > 100) throw new StocktakeWorkflowException("Từ khóa tối đa 100 ký tự.");
        var plans = await repo.Query<Plan>().AsNoTracking().Include(s => s.Warehouse)
            .Where(s => s.WarehouseId == actor.WarehouseId && (search == null || search == "" || s.StocktakeCode.Contains(search)))
            .OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        var ids = plans.Select(s => s.StocktakeId).ToArray();
        var assignments = await repo.Query<StocktakeAssignment>().AsNoTracking()
            .Where(a => ids.Contains(a.StocktakeId) && a.UserId == actorId && a.Status != "CANCELLED").ToListAsync(ct);
        var requests = await repo.Query<StocktakeRecountRequest>().AsNoTracking()
            .Where(r => ids.Contains(r.StocktakeId) && r.AssignedToUserId == actorId).ToListAsync(ct);
        var locations = await repo.Query<StorageLocation>().AsNoTracking()
            .Where(l => l.WarehouseId == actor.WarehouseId).ToListAsync(ct);
        var rows = new List<StocktakeTaskRow>();
        foreach (var plan in plans)
        {
            var own = assignments.Where(a => a.StocktakeId == plan.StocktakeId).ToList();
            if (own.Count != 0)
                rows.Add(new StocktakeTaskRow { Id = plan.StocktakeId, Code = plan.StocktakeCode,
                    Warehouse = plan.Warehouse.WarehouseName,
                    Status = plan.Status is "COMPLETED" or "CANCELLED" ? plan.Status
                        : own.All(a => a.Status == "DONE") ? "DONE"
                        : own.Any(a => a.Status == "IN_PROGRESS") ? "IN_PROGRESS" : "ASSIGNED",
                    PlannedAt = plan.PlannedAt,
                    Scope = string.Join("; ", own.Select(a => AssignmentLabel(a, locations))) });
            foreach (var request in requests.Where(r => r.StocktakeId == plan.StocktakeId).OrderByDescending(r => r.RequestedAt))
                rows.Add(new StocktakeTaskRow { Id = plan.StocktakeId, Code = plan.StocktakeCode,
                    Warehouse = plan.Warehouse.WarehouseName, Status = request.Status, PlannedAt = request.RequestedAt,
                    IsRecount = true, RecountRequestId = request.RecountRequestId,
                    Scope = $"Đếm lại sản phẩm #{request.ProductId} · " + RequestLabel(request, locations) });
        }
        page = Math.Clamp(page, 1, Math.Max(1, (rows.Count + StocktakeExecutionListViewModel.PageSize - 1) / StocktakeExecutionListViewModel.PageSize));
        var pageRows = rows.Skip((page - 1) * StocktakeExecutionListViewModel.PageSize)
            .Take(StocktakeExecutionListViewModel.PageSize).ToList();
        foreach (var row in pageRows.Where(row => !row.IsRecount))
        {
            var investigation = await repo.InvestigationForAsync(row.Id, ct);
            row.IsShortageSearch = investigation?.ShortageObservationIds.Count > 0;
            if (row.IsShortageSearch) row.Scope = "Toàn kho, gồm hàng chờ xếp";
        }
        return new StocktakeExecutionListViewModel { Page = page, TotalCount = rows.Count, Search = search,
            Items = pageRows };
    }

    public async Task<StocktakeExecutionViewModel> GetDetailsAsync(int actorId, long id, long? recountRequestId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        var plan = await repo.Query<Plan>().AsNoTracking().Include(s => s.Warehouse)
            .SingleOrDefaultAsync(s => s.StocktakeId == id && s.WarehouseId == actor.WarehouseId, ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy đợt kiểm kê trong kho được giao.", 404);
        var assignments = await OwnAssignmentsAsync(actorId, id, ct);
        var request = await OwnRequestAsync(actorId, id, recountRequestId, ct);
        EnsureAccess(assignments, request, recountRequestId);
        var locations = await LocationsAsync(plan.WarehouseId, ct);
        var scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => s.StocktakeId == id).ToListAsync(ct);
        var planLocations = StocktakeScopeRules.PlanLocations(plan, scopes, locations);
        var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == id)
            .OrderBy(r => r.RoundNumber).ToListAsync(ct);
        var current = request is null ? rounds.FirstOrDefault(r => r.RoundNumber == 1)
            : rounds.FirstOrDefault(r => r.RoundId == request.ResultRoundId);
        var observations = current is null ? new List<StocktakeObservation>() : await repo.Query<StocktakeObservation>()
            .AsNoTracking().Include(o => o.Product).Where(o => o.StocktakeId == id && o.RoundNumber == current.RoundNumber)
            .OrderBy(o => o.ScannedSerial).ToListAsync(ct);
        observations = observations.Where(o => request is not null
            ? true // Each recount round belongs exclusively to this request; cloned baseline may name an older area.
            : assignments.Any(a => o.ExpectedQuantity == 1 && StocktakeScopeRules.InAssignment(a, o.ExpectedLocationId, planLocations, locations)
                || (o.ActualQuantity == 1 && StocktakeScopeRules.InAssignment(a, o.ActualLocationId, planLocations, locations))))
            .ToList();
        var ownRunning = request is null ? assignments.Any(a => a.Status == "IN_PROGRESS") : request.Status == "IN_PROGRESS";
        var canCount = current?.Status == "OPEN" && ownRunning && plan.Status == (request is null ? "COUNTING" : "RECOUNTING");
        var investigation = await repo.InvestigationForAsync(id, ct);
        var shortageSearch = investigation?.ShortageObservationIds.Count > 0;
        if (current is null && investigation is not null && shortageSearch)
            observations = await repo.Query<StocktakeObservation>().AsNoTracking()
                .Where(o => o.StocktakeId == investigation.StocktakeId
                    && investigation.ShortageObservationIds.Contains(o.ObservationId))
                .OrderBy(o => o.ScannedSerial).ToListAsync(ct);
        return new StocktakeExecutionViewModel { Plan = plan, Assignments = assignments,
            IsShortageSearch = shortageSearch,
            Handling = (await repo.HandlingAsync(id, ct)).Where(h => h.RecordedByUserId == actorId).ToList(),
            Requests = request is null ? [] : [request], Rounds = rounds, Observations = observations,
            Locations = locations,
            AllowAwaitingPutaway = StocktakeScopeRules.InPlan(plan, planLocations, null) && (request is not null
                ? request.ScopeKind == "AWAITING_PUTAWAY"
                : assignments.Any(a => StocktakeScopeRules.InAssignment(a, null, planLocations, locations))),
            CountLocations = locations.Where(l => l.Status == "ACTIVE" && (request is not null
                ? StocktakeScopeRules.InRequest(request, request.ProductId, l.LocationId, locations)
                : assignments.Any(a => a.Status is "ASSIGNED" or "IN_PROGRESS"
                    && StocktakeScopeRules.InAssignment(a, l.LocationId, planLocations, locations)))).ToList(),
            Products = await repo.Query<Product>().AsNoTracking().OrderBy(p => p.ProductName).ToListAsync(ct),
            RecountRequestId = recountRequestId, ActorId = actorId, Version = Convert.ToBase64String(plan.RowVersion),
            ScopeLabel = shortageSearch ? "Toàn kho, gồm hàng chờ xếp"
                : request is null ? string.Join("; ", assignments.Select(a => AssignmentLabel(a, locations))) : RequestLabel(request, locations),
            CanStart = request is null
                ? plan.Status is "PLANNED" or "COUNTING" && assignments.Any(a => a.Status == "ASSIGNED")
                : plan.Status == "REVIEWING" && request.Status == "REQUESTED" && rounds.All(r => r.Status != "OPEN"),
            CanCount = canCount, CanSubmit = canCount };
    }

    public async Task StartAsync(int actorId, StocktakeActionInput input, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await repo.TransactionAsync(actorId, actor.WarehouseId!.Value, async token =>
        {
            var (plan, assignments, request, locations, planLocations) = await ContextAsync(actorId, input.Id, input.RecountRequestId, token);
            if (request?.Status is "IN_PROGRESS" or "SUBMITTED"
                || request is null && assignments.All(a => a.Status is "IN_PROGRESS" or "DONE")) return true;
            CheckVersion(plan, input.Version);
            await ValidateWarehouseAsync(plan.WarehouseId, token);
            if (request is not null)
            {
                if (request.Status != "REQUESTED" || plan.Status != "REVIEWING")
                    throw new StocktakeWorkflowException("Yêu cầu đếm lại chưa thể bắt đầu.");
                if (await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == plan.StocktakeId && r.Status == "OPEN", token))
                    throw new StocktakeWorkflowException("Đợt này đang có vòng đếm khác chưa kết thúc. Vui lòng chờ.", 409);
                var source = await repo.Query<StocktakeRound>().SingleAsync(r => r.RoundId == request.SourceRoundId, token);
                if (source.StocktakeId != plan.StocktakeId || source.Status != "CLOSED")
                    throw new StocktakeWorkflowException("Vòng nguồn chưa đóng hoặc không thuộc đợt kiểm kê.");
                await ValidateRecountAsync(actorId, plan, request, planLocations, locations, token);
                var now = DateTime.UtcNow;
                var round = new StocktakeRound { StocktakeId = plan.StocktakeId,
                    RoundNumber = await repo.Query<StocktakeRound>().Where(r => r.StocktakeId == plan.StocktakeId).MaxAsync(r => r.RoundNumber, token) + 1,
                    Status = "OPEN", StartedAt = now, StartedByUserId = actorId, RecountReason = request.Reason };
                repo.Add(round);
                await repo.SaveAsync(token);
                var previous = await repo.Query<StocktakeObservation>().AsNoTracking()
                    .Where(o => o.StocktakeId == plan.StocktakeId && o.ProductId == request.ProductId)
                    .OrderBy(o => o.RoundNumber).ToListAsync(token);
                var historyRounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == plan.StocktakeId).ToListAsync(token);
                var historyRequests = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == plan.StocktakeId).ToListAsync(token);
                var handled = (await repo.HandlingAsync(plan.StocktakeId, token)).Where(h => h.Status == "MOVED").ToList();
                var serials = StocktakeScopeRules.EffectiveObservations(previous, historyRounds, historyRequests)
                    .Where(o => StocktakeScopeRules.InRequest(request, o.ProductId,
                        handled.FirstOrDefault(h => h.ScannedSerial == o.ScannedSerial)?.TargetLocationId
                            ?? (o.ActualQuantity == 1 ? o.ActualLocationId : o.ExpectedLocationId), locations))
                    .Select(o => o.ScannedSerial).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var baseline in previous.Where(o => serials.Contains(o.ScannedSerial)).GroupBy(o => o.ScannedSerial, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
                    repo.Add(CloneBaseline(baseline, round.RoundNumber));
                request.Status = "IN_PROGRESS";
                request.ResultRoundId = round.RoundId;
                request.StartedAt = now;
                plan.Status = "RECOUNTING";
            }
            else
            {
                if (plan.Status is not ("PLANNED" or "COUNTING")) throw new StocktakeWorkflowException("Chỉ bắt đầu kế hoạch đã hoàn tất phân công.");
                if (plan.Status == "PLANNED")
                {
                    await ValidateStartAsync(plan, planLocations, locations, token);
                    if (await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == plan.StocktakeId, token)
                        || await repo.Query<StocktakeObservation>().AnyAsync(o => o.StocktakeId == plan.StocktakeId, token))
                        throw new StocktakeWorkflowException("Kế hoạch đã có dữ liệu thực hiện. Cần kiểm tra trước khi bắt đầu.");
                    var now = DateTime.UtcNow;
                    repo.Add(new StocktakeRound { StocktakeId = plan.StocktakeId, RoundNumber = 1, Status = "OPEN", StartedAt = now, StartedByUserId = actorId });
                    await repo.SaveAsync(token);
                    var physical = await repo.Query<SerialItem>().AsNoTracking()
                        .Where(s => s.CurrentWarehouseId == plan.WarehouseId && s.StockState == "IN_WAREHOUSE").ToListAsync(token);
                    var investigation = await repo.InvestigationForAsync(plan.StocktakeId, token);
                    if (investigation is not null && investigation.ShortageObservationIds.Count > 0)
                    {
                        var targets = await repo.Query<StocktakeObservation>().AsNoTracking()
                            .Where(o => o.StocktakeId == investigation.StocktakeId
                                && investigation.ShortageObservationIds.Contains(o.ObservationId)).ToListAsync(token);
                        physical = physical.Where(s => targets.Any(o => o.SerialItemId == s.SerialItemId
                            && o.ProductId == s.ProductId && o.ExpectedLocationId == s.CurrentLocationId
                            && o.ExpectedCondition == s.ItemCondition)).ToList();
                        if (physical.Count != investigation.ShortageObservationIds.Count)
                            throw new StocktakeWorkflowException("Dữ liệu sổ kho của serial cần tìm đã thay đổi. Báo Manager kiểm tra.");
                    }
                    foreach (var item in physical.Where(s => StocktakeScopeRules.InPlan(plan, planLocations, s.CurrentLocationId)))
                        repo.Add(new StocktakeObservation { StocktakeId = plan.StocktakeId, SerialItemId = item.SerialItemId,
                            ProductId = item.ProductId, ScannedSerial = item.SerialNumber, RoundNumber = 1,
                            ExpectedQuantity = 1, ActualQuantity = 0, ExpectedCondition = item.ItemCondition,
                            ExpectedLocationId = item.CurrentLocationId, DifferenceType = "PENDING", ReviewStatus = "PENDING", CapturedAt = now });
                    plan.Status = "COUNTING";
                    plan.StartedAt = now;
                }
                else if (!await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == plan.StocktakeId && r.RoundNumber == 1 && r.Status == "OPEN", token))
                    throw new StocktakeWorkflowException("Vòng đếm đầu tiên không còn mở.", 409);
                foreach (var assignment in assignments.Where(a => a.Status == "ASSIGNED")) assignment.Status = "IN_PROGRESS";
            }
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    public async Task ScanAsync(int actorId, StocktakeCountInput input, CancellationToken ct)
    {
        var serial = input.Serial?.Trim() ?? "";
        if (serial.Length is < 1 or > 100 || serial.Any(char.IsControl)) throw new StocktakeWorkflowException("Serial phải dài 1–100 ký tự và không chứa ký tự điều khiển.");
        if (input.Condition is not ("GOOD" or "DAMAGED")) throw new StocktakeWorkflowException("Chọn tình trạng GOOD hoặc DAMAGED.");
        if (input.Note?.Length > 1000) throw new StocktakeWorkflowException("Ghi chú tối đa 1.000 ký tự.");
        var actor = await ActorAsync(actorId, ct);
        await repo.TransactionAsync(actorId, actor.WarehouseId!.Value, async token =>
        {
            var (plan, assignments, request, locations, planLocations) = await ContextAsync(actorId, input.Id, input.RecountRequestId, token);
            CheckVersion(plan, input.Version);
            var round = await RunningRoundAsync(plan, assignments, request, token);
            if (input.LocationId is int location && !locations.Any(l => l.LocationId == location && l.Status == "ACTIVE" && l.LocationType is "TIER" or "FLOOR_SLOT"))
                throw new StocktakeWorkflowException("Vị trí thực tế phải là tầng kệ hoặc ô sàn đang hoạt động trong kho.");
            if (!StocktakeScopeRules.InPlan(plan, planLocations, input.LocationId)
                || (request is null ? !assignments.Any(a => a.Status == "IN_PROGRESS" && StocktakeScopeRules.InAssignment(a, input.LocationId, planLocations, locations))
                    : !StocktakeScopeRules.InRequest(request, request.ProductId, input.LocationId, locations)))
                throw new StocktakeWorkflowException("Vị trí thực tế nằm ngoài phạm vi được giao.", 403);
            var allPrevious = await repo.Query<StocktakeObservation>().AsNoTracking()
                .Where(o => o.StocktakeId == plan.StocktakeId && o.ScannedSerial == serial).OrderBy(o => o.RoundNumber).ToListAsync(token);
            var observation = await repo.Query<StocktakeObservation>().SingleOrDefaultAsync(o => o.StocktakeId == plan.StocktakeId
                && o.RoundNumber == round.RoundNumber && o.ScannedSerial == serial, token);
            var handling = (await repo.HandlingAsync(plan.StocktakeId, token)).SingleOrDefault(h => SameSerial(h.ScannedSerial, serial));
            if (handling?.Status == "MOVED" && (request is null || input.LocationId != handling.TargetLocationId || input.Condition != handling.ActualCondition))
                throw new StocktakeWorkflowException("Serial đã chuyển và xử lý trong đợt này; không ghi nhận trùng ở vị trí nhận.", 409);
            var item = await repo.Query<SerialItem>().AsNoTracking().SingleOrDefaultAsync(s => s.SerialNumber == serial, token);
            if (!StocktakeReceivingEvidence.IsInWarehouse(item, plan.WarehouseId))
            {
                var receipts = await StocktakeReceivingEvidence.FindAsync(repo, serial, plan.WarehouseId, "DRAFT", token);
                throw new StocktakeWorkflowException(StocktakeReceivingEvidence.BlockedMessage(item, receipts));
            }
            var productId = item!.ProductId;
            var investigation = await repo.InvestigationForAsync(plan.StocktakeId, token);
            var shortageSearch = investigation?.ShortageObservationIds.Count > 0;
            if (shortageSearch && observation?.SerialItemId != item.SerialItemId)
                throw new StocktakeWorkflowException("Serial này không nằm trong danh sách cần tìm.", 403);
            var previousProduct = observation?.ProductId ?? allPrevious.FirstOrDefault()?.ProductId;
            if (previousProduct.HasValue && previousProduct != productId)
                throw new StocktakeWorkflowException("Sản phẩm trong lần ghi nhận trước khác với serial trong sổ kho. Manager cần xác minh.");
            if (input.ProductId.HasValue && input.ProductId != productId) throw new StocktakeWorkflowException("Sản phẩm chọn không khớp serial đã biết.");
            if (request is not null && productId != request.ProductId) throw new StocktakeWorkflowException("Serial không thuộc sản phẩm được yêu cầu đếm lại.", 403);
            if (observation?.ScannedAt is not null)
            {
                if (observation.ScannedByUserId != actorId) throw new StocktakeWorkflowException("Serial đã được nhân viên khác ghi nhận. Manager cần rà soát trước khi thay đổi.", 409);
                if (observation.ActualCondition == input.Condition && observation.ActualLocationId == input.LocationId && observation.Note == input.Note
                    && !(observation.ExpectedQuantity == 0 && item is not null && observation.ReviewStatus != "RESOLVED")) return true;
            }
            if (observation is null)
            {
                var baseline = allPrevious.FirstOrDefault();
                observation = baseline is null ? new StocktakeObservation { StocktakeId = plan.StocktakeId,
                    SerialItemId = item?.SerialItemId, ProductId = productId, ScannedSerial = item?.SerialNumber ?? serial,
                    RoundNumber = round.RoundNumber, ExpectedQuantity = 0, CapturedAt = plan.StartedAt!.Value,
                    DifferenceType = "PENDING", ReviewStatus = "PENDING" } : CloneBaseline(baseline, round.RoundNumber);
                repo.Add(observation);
            }
            observation.ActualQuantity = 1;
            if (observation.SerialItemId is null && item is not null) observation.SerialItemId = item.SerialItemId;
            observation.ActualCondition = input.Condition;
            observation.ActualLocationId = input.LocationId;
            observation.ScannedAt = DateTime.UtcNow;
            observation.ScannedByUserId = actorId;
            observation.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
            observation.DifferenceType = Difference(observation);
            observation.ReviewStatus = handling?.Status == "MOVED" ? "RESOLVED" : "PENDING";
            if (item is not null && observation.ExpectedQuantity == 0 && item.CurrentWarehouseId == plan.WarehouseId
                && item.StockState == "IN_WAREHOUSE" && item.CurrentLocationId == input.LocationId && item.ItemCondition == input.Condition)
            {
                var firstUnknownAt = allPrevious.Where(o => o.SerialItemId == null && o.ScannedAt.HasValue)
                    .OrderBy(o => o.ScannedAt).FirstOrDefault()?.ScannedAt ?? observation.CapturedAt;
                if (item.RegisteredAt >= firstUnknownAt && await repo.Query<SupplySource>().AnyAsync(s => s.SupplySourceId == item.CurrentSupplySourceId
                    && s.SupplyState == "PHYSICAL" && s.ProductId == item.ProductId && s.WarehouseId == plan.WarehouseId && s.ItemCondition == item.ItemCondition
                    && s.SourceType == "INBOUND" && s.Inbound != null && s.Inbound.Status == "DONE" && s.Inbound.CompletedAt >= firstUnknownAt, token)
                    && await repo.Query<SerialMovement>().AnyAsync(m => m.SerialItemId == item.SerialItemId && m.EventType == "PURCHASE_RECEIPT"
                        && m.InboundId == item.OriginalInboundId && m.ToWarehouseId == plan.WarehouseId && m.OccurredAt >= firstUnknownAt, token))
                    observation.ReviewStatus = "RESOLVED"; // Proven receipt registered after the unknown observation: never add it twice.
            }
            await repo.SaveAsync(token);
            var investigationOnly = investigation is not null && investigation.ShortageObservationIds.Count == 0;
            // Finding a missing serial does not authorize changing its condition from damaged to good.
            var moveCondition = shortageSearch && item?.ItemCondition == "DAMAGED" ? "DAMAGED" : input.Condition;
            if (!investigationOnly && item is not null && item.CurrentWarehouseId == plan.WarehouseId && item.StockState == "IN_WAREHOUSE"
                && !(item.ItemCondition == "DAMAGED" && moveCondition == "GOOD")
                && (item.CurrentLocationId != input.LocationId || item.ItemCondition != moveCondition))
            {
                var isNewHandling = handling is null;
                if (handling is null)
                {
                    handling = new StocktakeHandling { HandlingId = -observation.ObservationId,
                        StocktakeId = plan.StocktakeId, ObservationId = observation.ObservationId,
                        SerialItemId = item.SerialItemId, ScannedSerial = item.SerialNumber, ProductId = item.ProductId,
                        OriginalLocationId = item.CurrentLocationId, RecordedByUserId = actorId, CreatedAt = DateTime.UtcNow };
                }
                handling.Status = "WAITING"; handling.ObservationId = observation.ObservationId;
                handling.RecordedByUserId = actorId;
                handling.FoundLocationId = input.LocationId; handling.ActualCondition = moveCondition; handling.Note = observation.Note;
                repo.Record(handling, actorId, isNewHandling);
            }
            else if (handling is { Status: "WAITING" })
            {
                handling.Status = "CANCELLED";
                repo.Record(handling, actorId, isNew: false);
            }
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    public async Task SubmitAsync(int actorId, StocktakeActionInput input, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await repo.TransactionAsync(actorId, actor.WarehouseId!.Value, async token =>
        {
            var (plan, assignments, request, locations, planLocations) = await ContextAsync(actorId, input.Id, input.RecountRequestId, token);
            var oldRecoveryObservationIds = repo.Query<StocktakeObservation>()
                .Where(o => o.StocktakeId == plan.StocktakeId && o.ExpectedCondition == "DAMAGED" && o.ActualCondition == "GOOD")
                .Select(o => o.ObservationId);
            var ignored = (await oldRecoveryObservationIds.ToListAsync(token)).ToHashSet();
            if ((await repo.HandlingAsync(plan.StocktakeId, token)).Any(h =>
                h.RecordedByUserId == actorId && h.Status == "WAITING" && !ignored.Contains(h.ObservationId)))
                throw new StocktakeWorkflowException("Còn hàng chờ chuyển đến vị trí hợp lệ. Xác nhận chuyển trước khi gửi kết quả.");
            if (request?.Status == "SUBMITTED" || request is null && assignments.All(a => a.Status == "DONE")) return true;
            CheckVersion(plan, input.Version);
            var round = await RunningRoundAsync(plan, assignments, request, token);
            var observations = await repo.Query<StocktakeObservation>().Where(o => o.StocktakeId == plan.StocktakeId
                && o.RoundNumber == round.RoundNumber).ToListAsync(token);
            var now = DateTime.UtcNow;
            if (request is null)
            {
                if (assignments.Any(a => a.Status == "ASSIGNED")) throw new StocktakeWorkflowException("Bạn cần bắt đầu phần việc được giao trước khi gửi kết quả.");
                foreach (var assignment in assignments.Where(a => a.Status == "IN_PROGRESS")) assignment.Status = "DONE";
                // Persist own assignments before checking completion of the shared first round.
                await repo.SaveAsync(token);
                var allDone = !await repo.Query<StocktakeAssignment>().AnyAsync(a => a.StocktakeId == plan.StocktakeId
                    && a.Status != "DONE" && a.Status != "CANCELLED", token);
                if (!allDone)
                {
                    repo.TouchPlan(plan);
                    await repo.SaveAsync(token);
                    return true;
                }
            }
            foreach (var observation in observations)
                observation.DifferenceType = observation.ActualQuantity == 0
                    ? observation.ExpectedQuantity == 1 ? "MISSING" : "MATCHED" : Difference(observation);
            // Flush results while OPEN. SQL forbids changing counts after closing the round.
            await repo.SaveAsync(token);
            round.Status = "CLOSED";
            round.ClosedAt = now;
            round.ClosedByUserId = actorId;
            if (request is not null) { request.Status = "SUBMITTED"; request.SubmittedAt = now; }
            plan.Status = "REVIEWING";
            repo.TouchPlan(plan);
            await repo.SaveAsync(token);
            return true;
        }, ct);
    }

    private async Task<(Plan Plan, List<StocktakeAssignment> Assignments, StocktakeRecountRequest? Request,
        List<StorageLocation> Locations, HashSet<int> PlanLocations)> ContextAsync(int actorId, long id, long? requestId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        var plan = await repo.Query<Plan>().SingleOrDefaultAsync(s => s.StocktakeId == id && s.WarehouseId == actor.WarehouseId, ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy đợt kiểm kê trong kho được giao.", 404);
        if (await repo.HasOpenInvestigationAsync(id, ct))
            throw new StocktakeWorkflowException("Đợt đang có cuộc đối soát toàn kho. Kết luận đối soát trước khi thay đổi kết quả gốc.", 409);
        var assignments = await OwnAssignmentsAsync(actorId, id, ct);
        var request = await OwnRequestAsync(actorId, id, requestId, ct);
        EnsureAccess(assignments, request, requestId);
        var locations = await LocationsAsync(plan.WarehouseId, ct);
        var scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => s.StocktakeId == id).ToListAsync(ct);
        return (plan, assignments, request, locations, StocktakeScopeRules.PlanLocations(plan, scopes, locations));
    }

    private Task<List<StocktakeAssignment>> OwnAssignmentsAsync(int actorId, long id, CancellationToken ct) =>
        repo.Query<StocktakeAssignment>().Where(a => a.StocktakeId == id && a.UserId == actorId && a.Status != "CANCELLED").ToListAsync(ct);

    private async Task<StocktakeRecountRequest?> OwnRequestAsync(int actorId, long id, long? requestId, CancellationToken ct)
    {
        if (!requestId.HasValue) return null;
        return await repo.Query<StocktakeRecountRequest>().SingleOrDefaultAsync(r => r.StocktakeId == id
            && r.RecountRequestId == requestId && r.AssignedToUserId == actorId, ct)
            ?? throw new StocktakeWorkflowException("Không tìm thấy yêu cầu đếm lại được giao cho bạn.", 404);
    }

    private static void EnsureAccess(List<StocktakeAssignment> assignments, StocktakeRecountRequest? request, long? requestId)
    {
        if (requestId is null && assignments.Count == 0 || requestId.HasValue && request is null)
            throw new StocktakeWorkflowException("Bạn không được phân công thực hiện nhiệm vụ này.", 403);
    }

    private Task<List<StorageLocation>> LocationsAsync(int warehouseId, CancellationToken ct) =>
        repo.Query<StorageLocation>().AsNoTracking().Where(l => l.WarehouseId == warehouseId).ToListAsync(ct);

    private async Task<StocktakeRound> RunningRoundAsync(Plan plan, List<StocktakeAssignment> assignments,
        StocktakeRecountRequest? request, CancellationToken ct)
    {
        if (request is null ? plan.Status != "COUNTING" || !assignments.Any(a => a.Status == "IN_PROGRESS")
            : plan.Status != "RECOUNTING" || request.Status != "IN_PROGRESS")
            throw new StocktakeWorkflowException("Nhiệm vụ chưa bắt đầu hoặc đã gửi kết quả.", 409);
        var isRecount = request is not null;
        var resultRoundId = request?.ResultRoundId;
        return await repo.Query<StocktakeRound>().SingleOrDefaultAsync(r => r.StocktakeId == plan.StocktakeId && r.Status == "OPEN"
            && (!isRecount ? r.RoundNumber == 1 : r.RoundId == resultRoundId), ct)
            ?? throw new StocktakeWorkflowException("Không có vòng đếm đang mở cho nhiệm vụ này.", 409);
    }

    private async Task ValidateWarehouseAsync(int warehouseId, CancellationToken ct)
    {
        var warehouse = await repo.Query<Models.Warehouse>().AsNoTracking().SingleAsync(w => w.WarehouseId == warehouseId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (warehouse.Status != "ACTIVE" || warehouse.WarehouseType == "RENTED"
            && (warehouse.RentalStartDate is null || warehouse.RentalEndDate is null || today < warehouse.RentalStartDate || today > warehouse.RentalEndDate))
            throw new StocktakeWorkflowException("Kho không hoạt động hoặc nằm ngoài thời hạn thuê.");
    }

    private async Task ValidateStartAsync(Plan plan, HashSet<int> planLocations, List<StorageLocation> locations, CancellationToken ct)
    {
        if (plan.ScopeType is not ("WHOLE_WAREHOUSE" or "LOCATIONS") || plan.ScopeType == "LOCATIONS" && planLocations.Count == 0)
            throw new StocktakeWorkflowException("Phạm vi kế hoạch không hợp lệ.");
        var assignments = await repo.Query<StocktakeAssignment>().Include(a => a.User).ThenInclude(u => u.Role)
            .Where(a => a.StocktakeId == plan.StocktakeId && a.Status != "CANCELLED").ToListAsync(ct);
        if (assignments.Count == 0 || assignments.Any(a => a.Status != "ASSIGNED" || a.User.Status != "ACTIVE"
            || a.User.Role.RoleName != "WAREHOUSE_STAFF" || a.User.WarehouseId != plan.WarehouseId))
            throw new StocktakeWorkflowException("Phân công cần có nhân viên kho đang hoạt động, đúng kho và chưa bắt đầu.");
        if (assignments.Any(a => a.ScopeKind switch
            {
                "LOCATION" => !a.LocationId.HasValue || !locations.Any(l => l.LocationId == a.LocationId)
                    || !StocktakeScopeRules.Descendants(a.LocationId.Value, locations).IsSubsetOf(planLocations),
                "AWAITING_PUTAWAY" => a.LocationId.HasValue || !StocktakeScopeRules.InPlan(plan, planLocations, null),
                "ALL_SCOPE" => a.LocationId.HasValue || assignments.Count != 1,
                _ => true
            }))
            throw new StocktakeWorkflowException("Phân công có phạm vi không hợp lệ hoặc nằm ngoài kế hoạch. Manager cần kiểm tra lại.");
        var leaves = locations.Where(l => planLocations.Contains(l.LocationId) && l.LocationType is "TIER" or "FLOOR_SLOT").ToList();
        if (leaves.Any(l => assignments.Count(a => StocktakeScopeRules.InAssignment(a, l.LocationId, planLocations, locations)) != 1)
            || StocktakeScopeRules.InPlan(plan, planLocations, null)
                && assignments.Count(a => StocktakeScopeRules.InAssignment(a, null, planLocations, locations)) != 1)
            throw new StocktakeWorkflowException("Phân công phải bao phủ đủ kế hoạch và không được chồng nhau.");
        var others = await repo.Query<Plan>().AsNoTracking().Where(s => s.WarehouseId == plan.WarehouseId
            && s.StocktakeId != plan.StocktakeId && LockedStatuses.Contains(s.Status)).ToListAsync(ct);
        var otherIds = others.Select(s => s.StocktakeId).ToArray();
        var investigation = await repo.InvestigationForAsync(plan.StocktakeId, ct);
        if (investigation?.ClosedAt is not null) investigation = null;
        if (investigation is not null)
        {
            if (plan.ScopeType != "WHOLE_WAREHOUSE" || !plan.IncludeAwaitingPutaway)
                throw new StocktakeWorkflowException("Đợt đối soát phải giữ phạm vi Toàn kho và bao gồm hàng chờ xếp. Manager cần sửa kế hoạch trước khi bắt đầu.");
            var parent = others.SingleOrDefault(p => p.StocktakeId == investigation.StocktakeId);
            if (parent is null || parent.Status != "REVIEWING"
                || (await repo.HandlingAsync(investigation.StocktakeId, ct)).Any(h => h.Status == "WAITING")
                || await repo.Query<StocktakeRound>().AnyAsync(r => r.StocktakeId == investigation.StocktakeId && r.Status == "OPEN", ct))
                throw new StocktakeWorkflowException("Đợt gốc cần hoàn tất đếm và xử lý hàng chờ chuyển trước khi đối soát toàn kho.");
        }
        var scopes = await repo.Query<StocktakeScope>().AsNoTracking().Where(s => otherIds.Contains(s.StocktakeId)).ToListAsync(ct);
        foreach (var other in others)
        {
            if (investigation?.StocktakeId == other.StocktakeId) continue; // Only the frozen source plan may overlap its linked investigation.
            var otherLocations = StocktakeScopeRules.PlanLocations(other, scopes.Where(s => s.StocktakeId == other.StocktakeId).ToList(), locations);
            if (other.ScopeType is not ("WHOLE_WAREHOUSE" or "LOCATIONS")
                || other.ScopeType == "LOCATIONS" && otherLocations.Count == 0)
                throw new StocktakeWorkflowException($"Đợt đang khóa {other.StocktakeCode} có phạm vi không hợp lệ. Cần rà soát trước khi bắt đầu đợt mới.", 409);
            if (otherLocations.Overlaps(planLocations) || StocktakeScopeRules.InPlan(other, otherLocations, null)
                && StocktakeScopeRules.InPlan(plan, planLocations, null))
                throw new StocktakeWorkflowException($"Phạm vi trùng với đợt kiểm kê đang khóa {other.StocktakeCode}.", 409);
        }
        var claimed = await repo.Query<PickingClaim>().AsNoTracking().Include(p => p.SerialItem)
            .Where(p => p.SerialItem.CurrentWarehouseId == plan.WarehouseId).ToListAsync(ct);
        if (claimed.Any(p => StocktakeScopeRules.InPlan(plan, planLocations, p.SerialItem.CurrentLocationId)))
            throw new StocktakeWorkflowException("Phạm vi đang có serial được giữ cho picking/xuất/chuyển kho. Chưa thể bắt đầu kiểm kê.", 409);
    }

    private async Task ValidateRecountAsync(int actorId, Plan plan, StocktakeRecountRequest request,
        HashSet<int> planLocations, List<StorageLocation> locations, CancellationToken ct)
    {
        if (request.ScopeKind == "LOCATION")
        {
            if (request.LocationId is not int locationId || !locations.Any(l => l.LocationId == locationId)
                || !StocktakeScopeRules.Descendants(locationId, locations).IsSubsetOf(planLocations))
                throw new StocktakeWorkflowException("Khu vực đếm lại không còn nằm trong phạm vi kế hoạch.");
        }
        else if (request.ScopeKind != "AWAITING_PUTAWAY" || !StocktakeScopeRules.InPlan(plan, planLocations, null))
            throw new StocktakeWorkflowException("Kế hoạch không bao gồm phạm vi hàng chờ xếp kệ này.");
        var rounds = await repo.Query<StocktakeRound>().AsNoTracking().Where(r => r.StocktakeId == plan.StocktakeId).ToListAsync(ct);
        var requests = await repo.Query<StocktakeRecountRequest>().AsNoTracking().Where(r => r.StocktakeId == plan.StocktakeId).ToListAsync(ct);
        var observations = await repo.Query<StocktakeObservation>().AsNoTracking()
            .Where(o => o.StocktakeId == plan.StocktakeId && o.ProductId == request.ProductId).ToListAsync(ct);
        var previous = StocktakeScopeRules.EffectiveObservations(observations, rounds, requests)
            .Where(o => StocktakeScopeRules.InRequest(request, o.ProductId,
                o.ActualQuantity == 1 ? o.ActualLocationId : o.ExpectedLocationId, locations)).ToList();
        var assignments = await repo.Query<StocktakeAssignment>().AsNoTracking()
            .Where(a => a.StocktakeId == plan.StocktakeId && a.Status != "CANCELLED").ToListAsync(ct);
        foreach (var observation in previous)
        {
            var priorRound = rounds.Single(r => r.RoundNumber == observation.RoundNumber);
            var previousCounter = observation.ScannedByUserId;
            if (previousCounter is null && observation.RoundNumber > 1)
                previousCounter = requests.SingleOrDefault(r => r.ResultRoundId == priorRound.RoundId)?.AssignedToUserId;
            if (previousCounter == actorId || previousCounter is null && assignments.Any(a => a.UserId == actorId
                && StocktakeScopeRules.InAssignment(a, observation.ExpectedLocationId, planLocations, locations)))
                throw new StocktakeWorkflowException("Người được giao đếm lại phải khác người đã đếm phần kết quả trước đó. Manager cần phân công lại.", 409);
            if (observation.RoundNumber > rounds.Single(r => r.RoundId == request.SourceRoundId).RoundNumber)
                throw new StocktakeWorkflowException("Phạm vi đã có kết quả mới hơn yêu cầu này. Manager cần rà soát lại yêu cầu.", 409);
        }
    }

    private static void CheckVersion(Plan plan, string version)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(version ?? ""); }
        catch (FormatException) { throw new StocktakeWorkflowException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.", 409); }
        if (!plan.RowVersion.SequenceEqual(supplied)) throw new StocktakeWorkflowException("Dữ liệu vừa thay đổi. Vui lòng tải lại trang trước khi thao tác.", 409);
    }

    private static StocktakeObservation CloneBaseline(StocktakeObservation source, int roundNumber) => new()
    {
        StocktakeId = source.StocktakeId, SerialItemId = source.SerialItemId, ProductId = source.ProductId,
        ScannedSerial = source.ScannedSerial, RoundNumber = roundNumber, ExpectedQuantity = source.ExpectedQuantity,
        ExpectedCondition = source.ExpectedCondition, ExpectedLocationId = source.ExpectedLocationId,
        CapturedAt = source.CapturedAt, ActualQuantity = 0, DifferenceType = "PENDING", ReviewStatus = "PENDING"
    };

    private static string Difference(StocktakeObservation observation)
    {
        if (observation.ExpectedQuantity == 0) return "SURPLUS";
        var condition = observation.ActualCondition != observation.ExpectedCondition;
        var location = observation.ActualLocationId != observation.ExpectedLocationId;
        return condition && location ? "COMBINED" : condition ? "DAMAGED" : location ? "WRONG_LOCATION" : "MATCHED";
    }

    private static string AssignmentLabel(StocktakeAssignment assignment, List<StorageLocation> locations) => assignment.ScopeKind switch
    {
        "ALL_SCOPE" => "Toàn bộ phạm vi kế hoạch",
        "AWAITING_PUTAWAY" => "Hàng chờ xếp kệ",
        _ => (locations.FirstOrDefault(l => l.LocationId == assignment.LocationId)?.LocationName ?? "Vị trí chưa xác định") + " và vị trí con"
    };

    private static string RequestLabel(StocktakeRecountRequest request, List<StorageLocation> locations) =>
        request.ScopeKind == "AWAITING_PUTAWAY" ? "Hàng chờ xếp kệ"
            : (locations.FirstOrDefault(l => l.LocationId == request.LocationId)?.LocationName ?? "Vị trí chưa xác định") + " và vị trí con";
}
