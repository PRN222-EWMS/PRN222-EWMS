using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class VwSupplyAvailability
{
    public long SupplySourceId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public string ItemCondition { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public string SupplyState { get; set; } = null!;

    public DateOnly? AvailableFrom { get; set; }

    public int RemainingQuantity { get; set; }

    public int EligibleQuantity { get; set; }

    public long ReservedBalance { get; set; }

    public long? UnreservedQuantity { get; set; }
}

