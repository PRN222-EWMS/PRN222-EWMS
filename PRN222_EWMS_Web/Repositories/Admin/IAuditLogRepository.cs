using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Repositories.Admin;

public interface IAuditLogRepository
{
    Task<int> CountAsync(string? tableName, string? actionType);
    Task<List<AuditLog>> GetPageAsync(string? tableName, string? actionType, int page, int pageSize);
    Task<AuditLog?> FindAsync(long id);
}
