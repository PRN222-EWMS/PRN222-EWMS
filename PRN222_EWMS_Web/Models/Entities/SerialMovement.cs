using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class SerialMovement
{
    public long MovementId { get; set; }

    public long SerialItemId { get; set; }

    public int PerformedByUserId { get; set; }

    public string EventType { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public string? FromState { get; set; }

    public string ToState { get; set; } = null!;

    public string? FromCondition { get; set; }

    public string ToCondition { get; set; } = null!;

    public int? FromWarehouseId { get; set; }

    public int? ToWarehouseId { get; set; }

    public int? FromLocationId { get; set; }

    public int? ToLocationId { get; set; }

    public bool? FromQuarantined { get; set; }

    public bool ToQuarantined { get; set; }

    public long? InboundId { get; set; }

    public long? OutboundId { get; set; }

    public long? ReturnReceiptId { get; set; }

    public long? DispatchId { get; set; }

    public long? TransferReceiptId { get; set; }

    public long? AdjustmentId { get; set; }

    public long? LossRequestId { get; set; }

    public string? Note { get; set; }

    public virtual InventoryAdjustment? Adjustment { get; set; }

    public virtual TransferDispatch? Dispatch { get; set; }

    public virtual StorageLocation? FromLocation { get; set; }

    public virtual Warehouse? FromWarehouse { get; set; }

    public virtual Inbound? Inbound { get; set; }

    public virtual TransferLossRequest? LossRequest { get; set; }

    public virtual Outbound? Outbound { get; set; }

    public virtual User PerformedByUser { get; set; } = null!;

    public virtual ReturnReceipt? ReturnReceipt { get; set; }

    public virtual SerialItem SerialItem { get; set; } = null!;

    public virtual StorageLocation? ToLocation { get; set; }

    public virtual Warehouse? ToWarehouse { get; set; }

    public virtual TransferReceipt? TransferReceipt { get; set; }
}
