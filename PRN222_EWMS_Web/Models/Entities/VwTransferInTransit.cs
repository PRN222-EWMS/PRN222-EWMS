using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class VwTransferInTransit
{
    public long DispatchItemId { get; set; }

    public long DispatchId { get; set; }

    public long TransferOrderId { get; set; }

    public long TransferLineId { get; set; }

    public long SerialItemId { get; set; }

    public string SerialNumber { get; set; } = null!;

    public int ProductId { get; set; }

    public string DispatchedCondition { get; set; } = null!;

    public int FromWarehouseId { get; set; }

    public int ToWarehouseId { get; set; }

    public DateTime? DispatchedAt { get; set; }
}
