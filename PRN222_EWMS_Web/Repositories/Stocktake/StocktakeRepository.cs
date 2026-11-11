using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Stocktaking;

public class StocktakeRepository(EwmsDbContext db) : IStocktakeRepository
{
    public IQueryable<T> Query<T>() where T : class => db.Set<T>();
    public void Add<T>(T entity) where T : class => db.Set<T>().Add(entity);
    public void TouchPlan(Models.Stocktake plan) => db.Entry(plan).Property(p => p.Note).IsModified = true;
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task<List<AuditLog>> ReadStocktakeHistoryAsync(string tableName, long? planId, CancellationToken ct)
    {
        var scope = planId.GetValueOrDefault();
        return db.Set<AuditLog>().FromSqlInterpolated($"""
            SELECT entry.*
            FROM dbo.AuditLog AS entry
            JOIN (
                SELECT MAX(a.AuditID) AS AuditID
                FROM dbo.AuditLog AS a
                WHERE a.TableName = {tableName} AND a.AfterData IS NOT NULL
                  AND ({scope} = 0
                    OR TRY_CONVERT(bigint, JSON_VALUE(a.AfterData, '$.StocktakeID')) = {scope}
                    OR TRY_CONVERT(bigint, JSON_VALUE(a.AfterData, '$.InvestigationStocktakeID')) = {scope})
                GROUP BY a.EntityKey
            ) AS latest ON latest.AuditID = entry.AuditID
            """).AsNoTracking().ToListAsync(ct);
    }

    public async Task<T> TransactionAsync<T>(int actorId, int warehouseId, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // Do not retry a partly executed callback against a dirty change tracker.
        await db.Database.OpenConnectionAsync(ct);
        object? previousActor = null;
        try
        {
            previousActor = await db.Database.SqlQuery<int?>($"SELECT TRY_CONVERT(int,SESSION_CONTEXT(N'ActorUserID')) AS [Value]").SingleAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Putaway and warehouse configuration acquire this same first lock.
            await db.Database.SqlQuery<int>($"SELECT WarehouseID AS [Value] FROM dbo.Warehouse WITH (UPDLOCK,HOLDLOCK) WHERE WarehouseID={warehouseId}").ToListAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}", ct);
            var result = await work(ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            try { await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={previousActor}", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}

public static class StocktakeScopeRules
{
    public static readonly string[] LockedStatuses = ["COUNTING", "RECOUNTING", "REVIEWING", "PENDING_APPROVAL"];

    public static HashSet<int> Descendants(int rootId, IReadOnlyCollection<StorageLocation> locations)
    {
        var result = new HashSet<int>();
        var pending = new Queue<int>();
        pending.Enqueue(rootId);
        while (pending.TryDequeue(out var id))
        {
            if (!result.Add(id)) continue;
            foreach (var child in locations.Where(l => l.ParentLocationId == id)) pending.Enqueue(child.LocationId);
        }
        return result;
    }

    public static HashSet<int> PlanLocations(Models.Stocktake plan, IReadOnlyCollection<StocktakeScope> scopes, IReadOnlyCollection<StorageLocation> locations)
    {
        var own = locations.Where(l => l.WarehouseId == plan.WarehouseId).ToList();
        return plan.ScopeType == "WHOLE_WAREHOUSE" ? own.Select(l => l.LocationId).ToHashSet()
            : scopes.Where(s => s.StocktakeId == plan.StocktakeId).SelectMany(s => Descendants(s.LocationId, own)).ToHashSet();
    }

    public static bool InPlan(Models.Stocktake plan, HashSet<int> scopeIds, int? locationId) =>
        locationId.HasValue ? scopeIds.Contains(locationId.Value) : plan.ScopeType == "WHOLE_WAREHOUSE" || plan.IncludeAwaitingPutaway;

    public static bool InAssignment(StocktakeAssignment assignment, int? locationId, HashSet<int> planLocations, IReadOnlyCollection<StorageLocation> locations) =>
        assignment.ScopeKind switch
        {
            "ALL_SCOPE" => locationId is null || planLocations.Contains(locationId.Value),
            "AWAITING_PUTAWAY" => locationId is null,
            "LOCATION" => locationId.HasValue && assignment.LocationId.HasValue && planLocations.Contains(locationId.Value)
                && Descendants(assignment.LocationId.Value, locations).Contains(locationId.Value),
            _ => false
        };

    public static bool InRequest(StocktakeRecountRequest request, int? productId, int? locationId, IReadOnlyCollection<StorageLocation> locations) =>
        productId == request.ProductId && (request.ScopeKind == "AWAITING_PUTAWAY" ? locationId is null
            : request.LocationId.HasValue && locationId.HasValue && Descendants(request.LocationId.Value, locations).Contains(locationId.Value));

    public static List<StocktakeObservation> EffectiveObservations(IReadOnlyCollection<StocktakeObservation> observations,
        IReadOnlyCollection<StocktakeRound> rounds, IReadOnlyCollection<StocktakeRecountRequest> requests)
    {
        var effectiveRounds = rounds.Where(r => r.Status == "CLOSED" && (r.RoundNumber == 1
            || requests.Any(q => q.ResultRoundId == r.RoundId && q.Status == "SUBMITTED"))).Select(r => r.RoundNumber).ToHashSet();
        return observations.Where(o => effectiveRounds.Contains(o.RoundNumber))
            .GroupBy(o => o.ScannedSerial, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(o => o.RoundNumber).First()).ToList();
    }
}
