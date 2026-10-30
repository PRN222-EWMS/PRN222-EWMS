using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class TransferDispatchItem
{
    public long DispatchItemId { get; set; }

    public long DispatchId { get; set; }

    public long TransferLineId { get; set; }

    public long SerialItemId { get; set; }

    public long SourceAtDispatchId { get; set; }

    public string DispatchedCondition { get; set; } = null!;

    public DateTime ScannedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual TransferDispatch Dispatch { get; set; } = null!;

    public virtual SerialItem SerialItem { get; set; } = null!;

    public virtual SupplySource SourceAtDispatch { get; set; } = null!;

    public virtual TransferOrderLine TransferLine { get; set; } = null!;

    public virtual TransferLossRequestItem? TransferLossRequestItem { get; set; }

    public virtual TransferReceiptItem? TransferReceiptItem { get; set; }
}
