using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Admin;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public class AuditLogService(IAuditLogRepository repository) : IAuditLogService
{
    private const int PageSize = 20;

    public async Task<AuditLogListViewModel> GetPageAsync(string? tableName, string? actionType, int page)
    {
        tableName = string.IsNullOrWhiteSpace(tableName) ? null : tableName.Trim();
        actionType = string.IsNullOrWhiteSpace(actionType) ? null : actionType.Trim().ToUpperInvariant();
        if (tableName?.Length > 128 || (actionType is not null && actionType is not ("INSERT" or "UPDATE" or "DELETE")))
            throw new ArgumentException("Bộ lọc nhật ký không hợp lệ.");

        var totalCount = await repository.CountAsync(tableName, actionType);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        return new AuditLogListViewModel
        {
            TableName = tableName, ActionType = actionType, Page = page,
            TotalPages = totalPages, TotalCount = totalCount,
            Logs = await repository.GetPageAsync(tableName, actionType, page, PageSize)
        };
    }

    public Task<AuditLog?> GetDetailsAsync(long id) => repository.FindAsync(id);
}
