using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class VwPurchaseOrderProgress
{
    public long PurchaseOrderId { get; set; }

    public string Pocode { get; set; } = null!;

    public int SupplierId { get; set; }

    public int WarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly ExpectedArrivalDate { get; set; }

    public long PolineId { get; set; }

    public int ProductId { get; set; }

    public int OrderedQuantity { get; set; }

    public int ClosedQuantity { get; set; }

    public long ReceivedQuantity { get; set; }

    public long? RemainingQuantity { get; set; }
}
