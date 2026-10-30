using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class SupplySource
{
    public long SupplySourceId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public string ItemCondition { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public string SupplyState { get; set; } = null!;

    public long? PolineId { get; set; }

    public long? InboundId { get; set; }

    public long? ReturnReceiptId { get; set; }

    public long? TransferReceiptId { get; set; }

    public long? AdjustmentId { get; set; }

    public int EligibleQuantity { get; set; }

    public int RemainingQuantity { get; set; }

    public DateOnly? AvailableFrom { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual InventoryAdjustment? Adjustment { get; set; }

    public virtual ICollection<DemandAllocation> DemandAllocations { get; set; } = new List<DemandAllocation>();

    public virtual Inbound? Inbound { get; set; }

    public virtual ICollection<OutboundItem> OutboundItemSourceAtIssues { get; set; } = new List<OutboundItem>();

    public virtual ICollection<OutboundItem> OutboundItemSupplySources { get; set; } = new List<OutboundItem>();

    public virtual PurchaseOrderLine? Poline { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<Reservation> ReservationSupplySourceNavigations { get; set; } = new List<Reservation>();

    public virtual ICollection<Reservation> ReservationSupplySources { get; set; } = new List<Reservation>();

    public virtual ReturnReceipt? ReturnReceipt { get; set; }

    public virtual ICollection<SerialItem> SerialItemCurrentSupplySources { get; set; } = new List<SerialItem>();

    public virtual ICollection<SerialItem> SerialItemSupplySources { get; set; } = new List<SerialItem>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItemSourceAtDispatches { get; set; } = new List<TransferDispatchItem>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItemSupplySources { get; set; } = new List<TransferDispatchItem>();

    public virtual TransferReceipt? TransferReceipt { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}

