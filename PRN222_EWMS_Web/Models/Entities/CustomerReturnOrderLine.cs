using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class CustomerReturnOrderLine
{
    public long ReturnLineId { get; set; }

    public long ReturnOrderId { get; set; }

    public long SolineId { get; set; }

    public int RequestedQuantity { get; set; }

    public int CancelledQuantity { get; set; }

    public virtual CustomerReturnOrder ReturnOrder { get; set; } = null!;

    public virtual ICollection<ReturnReceiptItem> ReturnReceiptItems { get; set; } = new List<ReturnReceiptItem>();

    public virtual SalesOrderLine Soline { get; set; } = null!;
}
