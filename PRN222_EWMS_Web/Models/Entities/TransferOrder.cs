using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class TransferOrder
{
    public long TransferOrderId { get; set; }

    public int FromWarehouseId { get; set; }

    public int ToWarehouseId { get; set; }

    public int CreatedByUserId { get; set; }

    public int? AssignedToUserId { get; set; }

    public string TransferCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public string ItemCondition { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    public virtual User? AssignedToUser { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Warehouse FromWarehouse { get; set; } = null!;

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual Warehouse ToWarehouse { get; set; } = null!;

    public virtual TransferDispatch? TransferDispatch { get; set; }

    public virtual ICollection<TransferDispatchItem> TransferDispatchItems { get; set; } = new List<TransferDispatchItem>();

    public virtual ICollection<TransferLossRequest> TransferLossRequests { get; set; } = new List<TransferLossRequest>();

    public virtual ICollection<TransferOrderLine> TransferOrderLines { get; set; } = new List<TransferOrderLine>();

    public virtual ICollection<TransferReceipt> TransferReceipts { get; set; } = new List<TransferReceipt>();
}
