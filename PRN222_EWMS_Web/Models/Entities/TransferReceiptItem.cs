using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class TransferReceiptItem
{
    public long TransferReceiptItemId { get; set; }

    public long TransferReceiptId { get; set; }

    public long DispatchItemId { get; set; }

    public string? ReceivedCondition { get; set; }

    public string? ConditionNote { get; set; }

    public DateTime ScannedAt { get; set; }

    public bool IsActive { get; set; }

    public long TransferOrderId { get; set; }

    public virtual TransferDispatchItem DispatchItem { get; set; } = null!;

    public virtual TransferDispatchItem TransferDispatchItem { get; set; } = null!;

    public virtual TransferReceipt TransferReceipt { get; set; } = null!;

    public virtual TransferReceipt TransferReceiptNavigation { get; set; } = null!;
}

