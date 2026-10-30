using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class TransferLossRequestItem
{
    public long LossItemId { get; set; }

    public long LossRequestId { get; set; }

    public long DispatchItemId { get; set; }

    public bool IsActive { get; set; }

    public string? Note { get; set; }

    public virtual TransferDispatchItem DispatchItem { get; set; } = null!;

    public virtual TransferLossRequest LossRequest { get; set; } = null!;
}
