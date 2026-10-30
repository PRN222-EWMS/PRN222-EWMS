using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class InventoryAlertThreshold
{
    public long ThresholdId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public int MinimumGoodQuantity { get; set; }

    public bool IsEnabled { get; set; }

    public int UpdatedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual User UpdatedByUser { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
