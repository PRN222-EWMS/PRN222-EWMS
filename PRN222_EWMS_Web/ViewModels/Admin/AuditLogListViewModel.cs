using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public class AuditLogListViewModel
{
    public int? ActorUserId { get; set; }
    public string? TableName { get; set; }
    public string? ActionType { get; set; }
    public string? DocumentCode { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public List<AuditLog> Logs { get; set; } = [];
    public List<AuditActorOption> Actors { get; set; } = [];
    public List<string> Tables { get; set; } = [];
}

public record AuditActorOption(int Id, string Name);

public class AuditLogFilter
{
    public int? ActorUserId { get; set; }
    public string? TableName { get; set; }
    public string? ActionType { get; set; }
    public string? DocumentCode { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
}
