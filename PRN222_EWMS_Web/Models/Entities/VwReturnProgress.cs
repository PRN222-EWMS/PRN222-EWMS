using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class VwReturnProgress
{
    public long ReturnOrderId { get; set; }

    public string ReturnCode { get; set; } = null!;

    public long SalesOrderId { get; set; }

    public int WarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public long ReturnLineId { get; set; }

    public long SolineId { get; set; }

    public int ProductId { get; set; }

    public int RequestedQuantity { get; set; }

    public int CancelledQuantity { get; set; }

    public long ReceivedQuantity { get; set; }

    public long? RemainingQuantity { get; set; }
}
