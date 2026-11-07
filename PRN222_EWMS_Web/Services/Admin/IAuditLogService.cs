using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public interface IAuditLogService
{
    Task<AuditLogListViewModel> GetPageAsync(string? tableName, string? actionType, int page);
    Task<AuditLog?> GetDetailsAsync(long id);
}
