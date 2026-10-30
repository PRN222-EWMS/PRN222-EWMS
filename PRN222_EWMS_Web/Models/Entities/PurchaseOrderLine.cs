using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class PurchaseOrderLine
{
    public long PolineId { get; set; }

    public long PurchaseOrderId { get; set; }

    public int ProductId { get; set; }

    public int OrderedQuantity { get; set; }

    public int ClosedQuantity { get; set; }

    public virtual ICollection<InboundItem> InboundItems { get; set; } = new List<InboundItem>();

    public virtual Product Product { get; set; } = null!;

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    public virtual SupplySource? SupplySource { get; set; }
}
