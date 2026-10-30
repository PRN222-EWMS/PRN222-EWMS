using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class CustomerReturnOrderLine
{
    public long ReturnLineId { get; set; }

    public long ReturnOrderId { get; set; }

    public long SolineId { get; set; }

    public int RequestedQuantity { get; set; }

    public int CancelledQuantity { get; set; }

    public long SalesOrderId { get; set; }

    public int ProductId { get; set; }

    public virtual CustomerReturnOrder CustomerReturnOrder { get; set; } = null!;

    public virtual CustomerReturnOrder ReturnOrder { get; set; } = null!;

    public virtual ICollection<ReturnReceiptItem> ReturnReceiptItemCustomerReturnOrderLines { get; set; } = new List<ReturnReceiptItem>();

    public virtual ICollection<ReturnReceiptItem> ReturnReceiptItemReturnLines { get; set; } = new List<ReturnReceiptItem>();

    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;

    public virtual SalesOrderLine Soline { get; set; } = null!;
}

