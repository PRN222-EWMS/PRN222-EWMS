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

    public long SalesOrderId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public string ItemCondition { get; set; } = null!;

    public string SupplyState { get; set; } = null!;

    public virtual Outbound Outbound { get; set; } = null!;

    public virtual Outbound OutboundNavigation { get; set; } = null!;

    public virtual ReturnReceiptItem? ReturnReceiptItemOutboundItem { get; set; }

    public virtual ICollection<ReturnReceiptItem> ReturnReceiptItemOutboundItemNavigations { get; set; } = new List<ReturnReceiptItem>();

    public virtual SalesOrder SalesOrder { get; set; } = null!;

    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;

    public virtual SerialItem SerialItem { get; set; } = null!;

    public virtual SerialItem SerialItemNavigation { get; set; } = null!;

    public virtual SalesOrderLine Soline { get; set; } = null!;

    public virtual SupplySource SourceAtIssue { get; set; } = null!;

    public virtual SupplySource SupplySource { get; set; } = null!;
}
