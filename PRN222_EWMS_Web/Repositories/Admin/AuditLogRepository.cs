using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Repositories.Admin;

public class AuditLogRepository(EwmsDbContext db) : IAuditLogRepository
{
    private IQueryable<AuditLog> Filter(AuditLogFilter filter)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (filter.ActorUserId.HasValue) query = query.Where(log => log.ActorUserId == filter.ActorUserId.Value);
        if (filter.TableName is not null) query = query.Where(log => log.TableName == filter.TableName);
        if (filter.ActionType is not null) query = query.Where(log => log.ActionType == filter.ActionType);
        if (filter.DocumentCode is not null) query = query.Where(log => log.EntityKey.Contains(filter.DocumentCode)
            || (log.BeforeData != null && log.BeforeData.Contains(filter.DocumentCode))
            || (log.AfterData != null && log.AfterData.Contains(filter.DocumentCode)));
        if (filter.FromDate.HasValue)
        {
            var start = AuditLogDisplay.ToUtcStart(filter.FromDate.Value);
            query = query.Where(log => log.OccurredAt >= start);
        }
        if (filter.ToDate.HasValue)
        {
            var end = AuditLogDisplay.ToUtcStart(filter.ToDate.Value.AddDays(1));
            query = query.Where(log => log.OccurredAt < end);
        }
        return query;
    }

    public Task<int> CountAsync(AuditLogFilter filter) => Filter(filter).CountAsync();

    public Task<List<AuditLog>> GetPageAsync(AuditLogFilter filter, int page, int pageSize) =>
        Filter(filter).Include(log => log.ActorUser)
            .OrderByDescending(log => log.OccurredAt).ThenByDescending(log => log.AuditId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    public Task<List<AuditActorOption>> GetActorsAsync() => db.Users.AsNoTracking()
        .Where(user => db.AuditLogs.Any(log => log.ActorUserId == user.UserId))
        .OrderBy(user => user.FullName).Select(user => new AuditActorOption(user.UserId, user.FullName)).ToListAsync();

    public Task<List<string>> GetTablesAsync() => db.AuditLogs.AsNoTracking()
        .Select(log => log.TableName).Distinct().OrderBy(name => name).ToListAsync();

    public Task<AuditLog?> FindAsync(long id) => db.AuditLogs.AsNoTracking().Include(log => log.ActorUser)
        .FirstOrDefaultAsync(log => log.AuditId == id);
}
