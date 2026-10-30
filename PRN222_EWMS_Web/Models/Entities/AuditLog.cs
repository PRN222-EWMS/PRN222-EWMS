using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public int? ActorUserId { get; set; }

    public string DatabasePrincipal { get; set; } = null!;

    public string TableName { get; set; } = null!;

    public string EntityKey { get; set; } = null!;

    public string ActionType { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public string? BeforeData { get; set; }

    public string? AfterData { get; set; }

    public string? Reason { get; set; }

    public string? RequestId { get; set; }

    public virtual User? ActorUser { get; set; }
}
