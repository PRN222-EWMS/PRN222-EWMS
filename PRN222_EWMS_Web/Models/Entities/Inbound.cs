using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class Inbound
{
    public long InboundId { get; set; }

    public long PurchaseOrderId { get; set; }

    public int CreatedByUserId { get; set; }

    public int? PerformedByUserId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public DateOnly ReceiptDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<InboundItem> InboundItemInboundNavigations { get; set; } = new List<InboundItem>();

    public virtual ICollection<InboundItem> InboundItemInbounds { get; set; } = new List<InboundItem>();

    public virtual User? PerformedByUser { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    public virtual ICollection<SerialItem> SerialItems { get; set; } = new List<SerialItem>();

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual ICollection<SupplySource> SupplySources { get; set; } = new List<SupplySource>();
}
