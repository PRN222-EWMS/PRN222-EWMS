using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class TransferLossRequest
{
    public long LossRequestId { get; set; }

    public long TransferOrderId { get; set; }

    public int SubmittedByUserId { get; set; }

    public int? DecidedByUserId { get; set; }

    public string RequestCode { get; set; } = null!;

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime? AppliedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? DecidedByUser { get; set; }

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual User SubmittedByUser { get; set; } = null!;

    public virtual ICollection<TransferLossRequestItem> TransferLossRequestItemLossRequests { get; set; } = new List<TransferLossRequestItem>();

    public virtual ICollection<TransferLossRequestItem> TransferLossRequestItemTransferLossRequests { get; set; } = new List<TransferLossRequestItem>();

    public virtual TransferOrder TransferOrder { get; set; } = null!;
}

