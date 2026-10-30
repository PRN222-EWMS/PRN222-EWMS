using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class TransferReceipt
{
    public long TransferReceiptId { get; set; }

    public long TransferOrderId { get; set; }

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

    public virtual User? PerformedByUser { get; set; }

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual ICollection<SupplySource> SupplySources { get; set; } = new List<SupplySource>();

    public virtual TransferOrder TransferOrder { get; set; } = null!;

    public virtual ICollection<TransferReceiptItem> TransferReceiptItems { get; set; } = new List<TransferReceiptItem>();
}
