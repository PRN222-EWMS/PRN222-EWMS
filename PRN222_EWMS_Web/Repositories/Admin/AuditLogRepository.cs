using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Admin;

public class AuditLogRepository(EwmsDbContext db) : IAuditLogRepository
{
    private IQueryable<AuditLog> Filter(string? tableName, string? actionType)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (tableName is not null) query = query.Where(log => log.TableName == tableName);
        if (actionType is not null) query = query.Where(log => log.ActionType == actionType);
        return query;
    }

    public Task<int> CountAsync(string? tableName, string? actionType) => Filter(tableName, actionType).CountAsync();

    public Task<List<AuditLog>> GetPageAsync(string? tableName, string? actionType, int page, int pageSize) =>
        Filter(tableName, actionType).Include(log => log.ActorUser)
            .OrderByDescending(log => log.OccurredAt).ThenByDescending(log => log.AuditId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    public Task<AuditLog?> FindAsync(long id) => db.AuditLogs.AsNoTracking().Include(log => log.ActorUser)
        .FirstOrDefaultAsync(log => log.AuditId == id);
}
