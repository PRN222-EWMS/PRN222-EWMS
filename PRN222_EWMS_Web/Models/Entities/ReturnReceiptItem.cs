using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class ReturnReceiptItem
{
    public long ReturnItemId { get; set; }

    public long ReturnReceiptId { get; set; }

    public long ReturnLineId { get; set; }

    public long OutboundItemId { get; set; }

    public string? ReceivedCondition { get; set; }

    public string? ConditionNote { get; set; }

    public DateTime ScannedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual OutboundItem OutboundItem { get; set; } = null!;

    public virtual CustomerReturnOrderLine ReturnLine { get; set; } = null!;

    public virtual ReturnReceipt ReturnReceipt { get; set; } = null!;
}
