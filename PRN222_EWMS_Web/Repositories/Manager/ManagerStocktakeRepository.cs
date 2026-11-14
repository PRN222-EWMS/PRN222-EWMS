using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Manager;

namespace PRN222_EWMS_Web.Repositories.Manager;

public class ManagerStocktakeRepository(EwmsDbContext db) : IManagerStocktakeRepository
{
    public Task<bool> IsActiveManagerAsync(int userId) => db.Users.AsNoTracking()
        .AnyAsync(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleName == "MANAGER");

    public Task<List<ManagerStocktakeWarehouseOption>> GetWarehousesAsync() => db.Warehouses.AsNoTracking()
        .OrderBy(w => w.WarehouseName)
        .Select(w => new ManagerStocktakeWarehouseOption(w.WarehouseId, w.WarehouseName, w.WarehouseType, w.Status,
            w.RentalStartDate, w.RentalEndDate)).ToListAsync();

    public async Task<ManagerStocktakePlanningData?> GetPlanningDataAsync(int warehouseId)
    {
        var warehouse = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouseId)
            .Select(w => new ManagerStocktakeWarehouseOption(w.WarehouseId, w.WarehouseName, w.WarehouseType, w.Status,
                w.RentalStartDate, w.RentalEndDate)).SingleOrDefaultAsync();
        if (warehouse is null) return null;

        var locations = await LocationOptions(warehouseId).ToListAsync();
        var staff = await db.Users.AsNoTracking()
            .Where(u => u.WarehouseId == warehouseId && u.Status == "ACTIVE" && u.Role.RoleName == "WAREHOUSE_STAFF")
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId)
            .Select(u => new ManagerStocktakeStaffOption(u.UserId, u.FullName)).ToListAsync();
        // A stocktake counts physical stock regardless of receipt source, condition or reservation.
        var awaiting = await db.SerialItems.AsNoTracking().CountAsync(s => s.CurrentWarehouseId == warehouseId
            && s.StockState == "IN_WAREHOUSE" && s.CurrentLocationId == null);
        return new ManagerStocktakePlanningData(warehouse, locations, staff, awaiting);
    }

    public async Task<ManagerStocktakeListViewModel> GetListAsync(ManagerStocktakeFilter filter)
    {
        var query = db.Stocktakes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(s => s.StocktakeCode.Contains(filter.Search));
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(s => s.Status == filter.Status);
        if (filter.WarehouseId.HasValue) query = query.Where(s => s.WarehouseId == filter.WarehouseId.Value);
        if (filter.FromDate.HasValue && filter.FromDate.Value > DateOnly.MinValue)
        {
            var from = ManagerStocktakeLabels.Utc(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(s => s.PlannedAt >= from);
        }
        if (filter.ToDate.HasValue && filter.ToDate.Value < DateOnly.MaxValue)
        {
            var until = ManagerStocktakeLabels.Utc(filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(s => s.PlannedAt < until);
        }

        var total = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, (total + ManagerStocktakeListViewModel.PageSize - 1) / ManagerStocktakeListViewModel.PageSize));
        var page = await query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.StocktakeId)
            .Skip((filter.Page - 1) * ManagerStocktakeListViewModel.PageSize).Take(ManagerStocktakeListViewModel.PageSize)
            .Select(s => new
            {
                Plan = s, WarehouseName = s.Warehouse.WarehouseName,
                HasExecution = db.StocktakeRounds.Any(r => r.StocktakeId == s.StocktakeId)
                    || db.StocktakeObservations.Any(o => o.StocktakeId == s.StocktakeId)
                    || db.InventoryAdjustments.Any(a => a.StocktakeId == s.StocktakeId)
                    || db.StocktakeAssignments.Any(a => a.StocktakeId == s.StocktakeId && (a.Status == "IN_PROGRESS" || a.Status == "DONE"))
            }).ToListAsync();
        var ids = page.Select(s => s.Plan.StocktakeId).ToArray();
        var scopeCounts = await db.StocktakeScopes.AsNoTracking().Where(s => ids.Contains(s.StocktakeId))
            .GroupBy(s => s.StocktakeId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(s => s.Id, s => s.Count);
        var assignments = await db.StocktakeAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.StocktakeId) && a.Status != "CANCELLED")
            .Select(a => new { a.StocktakeId, a.UserId, a.Status }).ToListAsync();
        var rounds = await db.StocktakeRounds.AsNoTracking().Where(r => ids.Contains(r.StocktakeId))
            .Select(r => new { r.StocktakeId, r.RoundNumber }).ToListAsync();
        var observations = await db.StocktakeObservations.AsNoTracking().Where(o => ids.Contains(o.StocktakeId))
            .GroupBy(o => new { o.StocktakeId, o.RoundNumber })
            .Select(g => new
            {
                g.Key.StocktakeId, g.Key.RoundNumber,
                Expected = g.Count(o => o.ExpectedQuantity == 1), Scanned = g.Count(o => o.ScannedAt != null)
            }).ToListAsync();

        var items = page.Select(row =>
        {
            var plan = row.Plan;
            var assigned = assignments.Where(a => a.StocktakeId == plan.StocktakeId).ToList();
            var latestRound = rounds.Where(r => r.StocktakeId == plan.StocktakeId).Select(r => (int?)r.RoundNumber).Max();
            var counts = observations.SingleOrDefault(o => o.StocktakeId == plan.StocktakeId && o.RoundNumber == latestRound);
            return new ManagerStocktakeListItem
            {
                Id = plan.StocktakeId, Code = plan.StocktakeCode, WarehouseId = plan.WarehouseId, WarehouseName = row.WarehouseName,
                Status = plan.Status, ScopeType = plan.ScopeType, IncludeAwaitingPutaway = plan.IncludeAwaitingPutaway,
                PlannedAt = plan.PlannedAt, StartedAt = plan.StartedAt, ScopeCount = scopeCounts.GetValueOrDefault(plan.StocktakeId),
                StaffCount = assigned.Select(a => a.UserId).Distinct().Count(), AssignmentCount = assigned.Count,
                CompletedAssignmentCount = assigned.Count(a => a.Status == "DONE"), LatestRoundNumber = latestRound,
                ExpectedCount = counts?.Expected ?? 0, ScannedCount = counts?.Scanned ?? 0,
                CanEdit = (plan.Status is "DRAFT" or "PLANNED") && plan.StartedAt is null && !row.HasExecution
            };
        }).ToList();
        return new ManagerStocktakeListViewModel { Filter = filter, TotalCount = total, Items = items, Warehouses = await GetWarehousesAsync() };
    }

    public async Task<ManagerStocktakeDetailViewModel?> GetDetailsAsync(long id, ManagerStocktakeDetailFilter filter)
    {
        var plan = await db.Stocktakes.AsNoTracking().Include(s => s.Warehouse).Include(s => s.CreatedByUser)
            .SingleOrDefaultAsync(s => s.StocktakeId == id);
        if (plan is null) return null;

        var locations = await LocationOptions(plan.WarehouseId).ToListAsync();
        var paths = BuildPaths(locations);
        var scopes = await GetScopesAsync(id);
        var assignments = await GetAssignmentsAsync(id);
        var rounds = await db.StocktakeRounds.AsNoTracking().Where(r => r.StocktakeId == id)
            .OrderBy(r => r.RoundNumber)
            .Select(r => new ManagerStocktakeRoundView(r.RoundNumber, r.Status, r.StartedAt, r.ClosedAt, r.StartedByUser.FullName, r.RecountReason))
            .ToListAsync();
        var hasExecution = await HasExecutionDataAsync(id);
        var editable = (plan.Status is "DRAFT" or "PLANNED") && plan.StartedAt is null && !hasExecution;
        var model = new ManagerStocktakeDetailViewModel
        {
            Id = id, Code = plan.StocktakeCode, WarehouseId = plan.WarehouseId, WarehouseName = plan.Warehouse.WarehouseName,
            Creator = plan.CreatedByUser.FullName, CreatedAt = plan.CreatedAt, Status = plan.Status, ScopeType = plan.ScopeType,
            IncludeAwaitingPutaway = plan.IncludeAwaitingPutaway, PlannedAt = plan.PlannedAt, StartedAt = plan.StartedAt,
            ClosedAt = plan.ClosedAt, Note = plan.Note, RowVersion = Convert.ToBase64String(plan.RowVersion),
            CanEdit = editable, CanDelete = editable, Filter = filter, Rounds = rounds,
            ScopePaths = scopes.Select(s => Path(paths, s.LocationId)).ToList(),
            Assignments = assignments.Select(a => new ManagerStocktakeAssignmentView(a.AssignmentId, a.UserId, a.User.FullName,
                a.ScopeKind, a.ScopeKind switch
                {
                    "ALL_SCOPE" => "Toàn bộ phạm vi đợt",
                    "AWAITING_PUTAWAY" => "Hàng chờ xếp kho",
                    _ => a.LocationId.HasValue ? Path(paths, a.LocationId.Value) : "Phân công thiếu vị trí"
                }, a.Status, a.AssignedAt)).ToList()
        };
        if ((plan.Status is "DRAFT" or "PLANNED") && plan.IncludeAwaitingPutaway
            && assignments.All(a => a.Status == "CANCELLED" || a.ScopeKind is not ("ALL_SCOPE" or "AWAITING_PUTAWAY")))
            model.Warnings.Add("Kế hoạch có hàng chờ xếp nhưng chưa phân công người kiểm phần này. Hãy bổ sung trước khi bắt đầu.");

        model.OriginalCapturedAt = await db.StocktakeObservations.AsNoTracking().Where(o => o.StocktakeId == id && o.RoundNumber == 1)
            .Select(o => (DateTime?)o.CapturedAt).MinAsync();
        if (rounds.Count == 0) return model;

        if (filter.RoundNumber.HasValue && rounds.All(r => r.Number != filter.RoundNumber.Value))
            throw new ArgumentException("Vòng kiểm kê đã chọn không tồn tại.", nameof(filter.RoundNumber));
        filter.RoundNumber ??= rounds[^1].Number;
        var round = rounds.Single(r => r.Number == filter.RoundNumber.Value);
        var roundQuery = db.StocktakeObservations.AsNoTracking().Where(o => o.StocktakeId == id && o.RoundNumber == round.Number);
        model.ExpectedCount = await roundQuery.CountAsync(o => o.ExpectedQuantity == 1);
        model.ScannedCount = await roundQuery.CountAsync(o => o.ScannedAt != null);
        var effective = roundQuery.Select(o => new
        {
            Observation = o,
            Difference = round.Status == "OPEN" && o.ExpectedQuantity == 1 && o.ActualQuantity == 0 && o.ScannedAt == null
                ? "PENDING" : o.DifferenceType
        });
        model.DiscrepancyCount = await effective.CountAsync(o => o.Difference != "PENDING" && o.Difference != "MATCHED");
        if (!string.IsNullOrWhiteSpace(filter.Search)) effective = effective.Where(o => o.Observation.ScannedSerial.Contains(filter.Search)
            || (o.Observation.Product != null && (o.Observation.Product.ProductName.Contains(filter.Search)
                || o.Observation.Product.ProductCode.Contains(filter.Search))));
        if (!string.IsNullOrWhiteSpace(filter.DifferenceType)) effective = effective.Where(o => o.Difference == filter.DifferenceType);
        model.ObservationCount = await effective.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, model.TotalPages);
        var rows = await effective.OrderBy(o => o.Observation.ScannedSerial).ThenBy(o => o.Observation.ObservationId)
            .Skip((filter.Page - 1) * ManagerStocktakeDetailViewModel.PageSize).Take(ManagerStocktakeDetailViewModel.PageSize)
            .Select(o => new
            {
                Item = o.Observation, o.Difference, Product = o.Observation.Product == null ? null : o.Observation.Product.ProductName,
                Scanner = o.Observation.ScannedByUser == null ? null : o.Observation.ScannedByUser.FullName
            }).ToListAsync();

        // Actual locations can be outside the expected warehouse when recording a discrepancy.
        var extraIds = rows.SelectMany(r => new[] { r.Item.ExpectedLocationId, r.Item.ActualLocationId })
            .Where(value => value.HasValue && !paths.ContainsKey(value.Value)).Select(value => value!.Value).Distinct().ToArray();
        if (extraIds.Length > 0)
        {
            var otherWarehouses = await db.StorageLocations.AsNoTracking().Where(l => extraIds.Contains(l.LocationId))
                .Select(l => l.WarehouseId).Distinct().ToListAsync();
            foreach (var warehouseId in otherWarehouses)
            {
                var warehouseName = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouseId)
                    .Select(w => w.WarehouseName).SingleAsync();
                var otherPaths = BuildPaths(await LocationOptions(warehouseId).ToListAsync());
                foreach (var pair in otherPaths) paths.TryAdd(pair.Key, $"{warehouseName} / {pair.Value}");
            }
        }
        model.Observations = rows.Select(r => new ManagerStocktakeObservationView(r.Item.ObservationId, r.Item.ScannedSerial,
            r.Product, r.Item.ExpectedQuantity, r.Item.ActualQuantity, r.Item.ExpectedCondition, r.Item.ActualCondition,
            r.Item.ExpectedLocationId.HasValue ? Path(paths, r.Item.ExpectedLocationId.Value)
                : r.Item.ExpectedQuantity == 1 ? "Hàng chờ xếp kho" : "Không có trong snapshot",
            r.Item.ActualLocationId.HasValue ? Path(paths, r.Item.ActualLocationId.Value)
                : r.Item.ActualQuantity == 1 ? "Hàng chờ xếp kho" : "Chưa ghi nhận",
            r.Difference, r.Item.CapturedAt, r.Item.ScannedAt, r.Scanner, r.Item.ReviewStatus, r.Item.Note)).ToList();
        return model;
    }

    public Task<Stocktake?> FindAsync(long id, bool forUpdate)
    {
        if (forUpdate && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Locking a stocktake requires a transaction.");
        var query = forUpdate
            ? db.Stocktakes.FromSqlInterpolated($"SELECT * FROM dbo.Stocktake WITH (UPDLOCK,HOLDLOCK) WHERE StocktakeID={id}")
            : db.Stocktakes;
        return query.AsTracking().SingleOrDefaultAsync(s => s.StocktakeId == id);
    }

    public Task<List<StocktakeScope>> GetScopesAsync(long id) => db.StocktakeScopes.AsNoTracking()
        .Where(s => s.StocktakeId == id).OrderBy(s => s.ScopeId).ToListAsync();

    public Task<List<StocktakeAssignment>> GetAssignmentsAsync(long id) => db.StocktakeAssignments.AsNoTracking()
        .Where(a => a.StocktakeId == id).Include(a => a.User).OrderBy(a => a.AssignmentId).ToListAsync();

    public async Task<bool> HasExecutionDataAsync(long id) =>
        await db.StocktakeRounds.AsNoTracking().AnyAsync(r => r.StocktakeId == id)
        || await db.StocktakeObservations.AsNoTracking().AnyAsync(o => o.StocktakeId == id)
        || await db.InventoryAdjustments.AsNoTracking().AnyAsync(a => a.StocktakeId == id)
        || await db.StocktakeAssignments.AsNoTracking().AnyAsync(a => a.StocktakeId == id && (a.Status == "IN_PROGRESS" || a.Status == "DONE"));

    public async Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> operation)
    {
        await db.Database.OpenConnectionAsync();
        object? previousActor = null;
        var contextChanged = false;
        Exception? operationFailure = null;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT SESSION_CONTEXT(N'ActorUserID')";
                previousActor = await command.ExecuteScalarAsync();
            }
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}");
            contextChanged = true;
            var result = await operation();
            // Cleanup must succeed before commit, otherwise the operation rolls back.
            var previousActorParameter = new SqlParameter("@previousActor", SqlDbType.Variant) { Value = previousActor ?? DBNull.Value };
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=@previousActor",
                new object[] { previousActorParameter });
            contextChanged = false;
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception error)
        {
            operationFailure = error;
            throw;
        }
        finally
        {
            try
            {
                if (contextChanged)
                {
                    var parameter = new SqlParameter("@previousActor", SqlDbType.Variant) { Value = previousActor ?? DBNull.Value };
                    await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=@previousActor",
                        new object[] { parameter });
                }
            }
            catch (Exception restoreFailure)
            {
                // A connection with un-restored audit context must not return to the pool.
                if (db.Database.GetDbConnection() is SqlConnection connection) SqlConnection.ClearPool(connection);
                if (operationFailure is not null)
                    throw new AggregateException("Stocktake operation and audit-context cleanup failed.", operationFailure, restoreFailure);
                throw;
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    public async Task InsertHeaderAsync(Stocktake plan)
    {
        RequireTransaction();
        db.Stocktakes.Add(plan);
        await db.SaveChangesAsync();
    }

    public async Task SaveHeaderAsync(Stocktake plan)
    {
        RequireTransaction();
        // Updating children alone does not advance the header's RowVersion.
        db.Entry(plan).Property(s => s.Note).IsModified = true;
        await db.SaveChangesAsync();
    }

    public async Task ClearPlanChildrenAsync(long id)
    {
        RequireTransaction();
        await db.StocktakeAssignments.Where(a => a.StocktakeId == id).ExecuteDeleteAsync();
        await db.StocktakeScopes.Where(s => s.StocktakeId == id).ExecuteDeleteAsync();
    }

    public async Task InsertPlanChildrenAsync(Stocktake plan, IReadOnlyCollection<int> scopeIds,
        IReadOnlyCollection<ManagerStocktakeAssignmentInput> assignments)
    {
        RequireTransaction();
        db.StocktakeScopes.AddRange(scopeIds.Select(id => new StocktakeScope
        {
            StocktakeId = plan.StocktakeId, WarehouseId = plan.WarehouseId, LocationId = id
        }));
        var assignedAt = DateTime.UtcNow;
        db.StocktakeAssignments.AddRange(assignments.Select(a => new StocktakeAssignment
        {
            StocktakeId = plan.StocktakeId, WarehouseId = plan.WarehouseId, UserId = a.UserId,
            LocationId = a.LocationId, ScopeKind = a.ScopeKind, Status = "ASSIGNED", AssignedAt = assignedAt
        }));
        await db.SaveChangesAsync();
    }

    public async Task DeleteHeaderAsync(Stocktake plan)
    {
        RequireTransaction();
        db.Stocktakes.Remove(plan);
        await db.SaveChangesAsync();
    }

    private IQueryable<ManagerStocktakeLocationOption> LocationOptions(int warehouseId) => db.StorageLocations.AsNoTracking()
        .Where(l => l.WarehouseId == warehouseId).OrderBy(l => l.LocationName).ThenBy(l => l.LocationId)
        .Select(l => new ManagerStocktakeLocationOption
        {
            Id = l.LocationId, ParentId = l.ParentLocationId, Name = l.LocationName, Type = l.LocationType, Status = l.Status
        });

    private static Dictionary<int, string> BuildPaths(List<ManagerStocktakeLocationOption> locations)
    {
        var lookup = locations.ToDictionary(l => l.Id);
        var result = new Dictionary<int, string>();
        foreach (var location in locations)
        {
            var parts = new List<string>();
            var visited = new HashSet<int>();
            var cursor = location;
            while (true)
            {
                if (!visited.Add(cursor.Id)) { parts.Add("[Cấu trúc lặp]"); break; }
                parts.Add(cursor.Name);
                if (cursor.ParentId is not int parentId) break;
                if (!lookup.TryGetValue(parentId, out var parent)) { parts.Add($"[Vị trí cha #{parentId}]"); break; }
                cursor = parent;
            }
            parts.Reverse();
            result[location.Id] = string.Join(" / ", parts);
        }
        return result;
    }

    private static string Path(IReadOnlyDictionary<int, string> paths, int id) => paths.TryGetValue(id, out var path) ? path : $"Vị trí #{id}";

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Stocktake writes require a transaction.");
    }
}
