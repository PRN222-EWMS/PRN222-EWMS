using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Repositories.Admin;

public interface IAuditLogRepository
{
    Task<int> CountAsync(AuditLogFilter filter);
    Task<List<AuditLog>> GetPageAsync(AuditLogFilter filter, int page, int pageSize);
    Task<List<AuditActorOption>> GetActorsAsync();
    Task<List<string>> GetTablesAsync();
    Task<AuditLog?> FindAsync(long id);
}
