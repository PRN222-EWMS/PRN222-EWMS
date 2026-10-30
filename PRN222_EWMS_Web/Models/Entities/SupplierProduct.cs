using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class SupplierProduct
{
    public long SupplierProductId { get; set; }

    public int SupplierId { get; set; }

    public int ProductId { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
}
