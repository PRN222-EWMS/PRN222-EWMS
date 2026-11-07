using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.Admin;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public class AuditLogService(IAuditLogRepository repository) : IAuditLogService
{
    private const int PageSize = 20;

    public async Task<AuditLogListViewModel> GetPageAsync(AuditLogFilter filter)
    {
        filter.TableName = string.IsNullOrWhiteSpace(filter.TableName) ? null : filter.TableName.Trim();
        filter.ActionType = string.IsNullOrWhiteSpace(filter.ActionType) ? null : filter.ActionType.Trim().ToUpperInvariant();
        filter.DocumentCode = string.IsNullOrWhiteSpace(filter.DocumentCode) ? null : filter.DocumentCode.Trim();
        if (filter.TableName?.Length > 128 || filter.DocumentCode?.Length > 100 || filter.ActorUserId < 1
            || (filter.ActionType is not null && filter.ActionType is not ("INSERT" or "UPDATE" or "DELETE"))
            || filter.FromDate > filter.ToDate)
            throw new ArgumentException("Bộ lọc nhật ký không hợp lệ.");

        var totalCount = await repository.CountAsync(filter);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);
        return new AuditLogListViewModel
        {
            ActorUserId = filter.ActorUserId, TableName = filter.TableName, ActionType = filter.ActionType,
            DocumentCode = filter.DocumentCode, FromDate = filter.FromDate, ToDate = filter.ToDate, Page = page,
            TotalPages = totalPages, TotalCount = totalCount,
            Logs = await repository.GetPageAsync(filter, page, PageSize),
            Actors = await repository.GetActorsAsync(), Tables = await repository.GetTablesAsync()
        };
    }

    public Task<AuditLog?> GetDetailsAsync(long id) => repository.FindAsync(id);
}
