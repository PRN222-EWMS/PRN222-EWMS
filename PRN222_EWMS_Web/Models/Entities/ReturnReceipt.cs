using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class ReturnReceipt
{
    public long ReturnReceiptId { get; set; }

    public long ReturnOrderId { get; set; }

    public long OutboundId { get; set; }

    public int CreatedByUserId { get; set; }

    public int? PerformedByUserId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public DateOnly ReceiptDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public long SalesOrderId { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual CustomerReturnOrder CustomerReturnOrder { get; set; } = null!;

    public virtual Outbound Outbound { get; set; } = null!;

    public virtual Outbound OutboundNavigation { get; set; } = null!;

    public virtual User? PerformedByUser { get; set; }

    public virtual CustomerReturnOrder ReturnOrder { get; set; } = null!;

    public virtual ICollection<ReturnReceiptItem> ReturnReceiptItemReturnReceiptNavigations { get; set; } = new List<ReturnReceiptItem>();

    public virtual ICollection<ReturnReceiptItem> ReturnReceiptItemReturnReceipts { get; set; } = new List<ReturnReceiptItem>();

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual ICollection<SupplySource> SupplySources { get; set; } = new List<SupplySource>();
}
