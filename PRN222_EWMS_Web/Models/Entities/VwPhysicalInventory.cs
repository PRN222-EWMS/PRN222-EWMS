using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class VwPhysicalInventory
{
    public int ProductId { get; set; }

    public int? WarehouseId { get; set; }

    public string ItemCondition { get; set; } = null!;

    public long? OnHandQuantity { get; set; }

    public long? AwaitingPutawayQuantity { get; set; }

    public long? QuarantinedQuantity { get; set; }

    public long? EligiblePhysicalQuantity { get; set; }
}
