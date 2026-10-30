using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class VwLowStockAlert
{
    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public int MinimumGoodQuantity { get; set; }

    public long GoodPhysicalQuantity { get; set; }
}
