using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class AuditLogListViewModel
{
    public string? TableName { get; set; }
    public string? ActionType { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public List<AuditLog> Logs { get; set; } = [];
}
