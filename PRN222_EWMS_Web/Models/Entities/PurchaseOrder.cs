using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class PurchaseOrder
{
    public long PurchaseOrderId { get; set; }

    public int SupplierId { get; set; }

    public int WarehouseId { get; set; }

    public int CreatedByUserId { get; set; }

    public string Pocode { get; set; } = null!;

    public DateOnly OrderDate { get; set; }

    public DateOnly? ExpectedArrivalDate { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<Inbound> Inbounds { get; set; } = new List<Inbound>();

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
