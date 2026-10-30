using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class OutboundItem
{
    public long OutboundItemId { get; set; }

    public long OutboundId { get; set; }

    public long SolineId { get; set; }

    public long SerialItemId { get; set; }

    public long SourceAtIssueId { get; set; }

    public DateTime ScannedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual Outbound Outbound { get; set; } = null!;

    public virtual ReturnReceiptItem? ReturnReceiptItem { get; set; }

    public virtual SerialItem SerialItem { get; set; } = null!;

    public virtual SalesOrderLine Soline { get; set; } = null!;

    public virtual SupplySource SourceAtIssue { get; set; } = null!;
}
