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

    public long TransferOrderId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public string SupplyState { get; set; } = null!;

    public virtual TransferDispatch Dispatch { get; set; } = null!;

    public virtual SerialItem SerialItem { get; set; } = null!;

    public virtual SerialItem SerialItemNavigation { get; set; } = null!;

    public virtual SupplySource SourceAtDispatch { get; set; } = null!;

    public virtual SupplySource SupplySource { get; set; } = null!;

    public virtual TransferDispatch TransferDispatch { get; set; } = null!;

    public virtual TransferOrderLine TransferLine { get; set; } = null!;

    public virtual TransferLossRequestItem? TransferLossRequestItemDispatchItem { get; set; }

    public virtual ICollection<TransferLossRequestItem> TransferLossRequestItemTransferDispatchItems { get; set; } = new List<TransferLossRequestItem>();

    public virtual TransferOrder TransferOrder { get; set; } = null!;

    public virtual TransferOrderLine TransferOrderLine { get; set; } = null!;

    public virtual TransferReceiptItem? TransferReceiptItemDispatchItem { get; set; }

    public virtual ICollection<TransferReceiptItem> TransferReceiptItemTransferDispatchItems { get; set; } = new List<TransferReceiptItem>();
}
