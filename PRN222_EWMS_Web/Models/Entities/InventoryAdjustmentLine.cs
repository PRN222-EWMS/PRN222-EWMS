using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class InventoryAdjustmentLine
{
    public long AdjustmentLineId { get; set; }

    public long AdjustmentId { get; set; }

    public long ObservationId { get; set; }

    public long? SerialItemId { get; set; }

    public int? ProductId { get; set; }

    public string AdjustmentType { get; set; } = null!;

    public int QuantityDelta { get; set; }

    public string? TargetCondition { get; set; }

    public int? TargetLocationId { get; set; }

    public bool IsActive { get; set; }

    public string? Note { get; set; }

    public long StocktakeId { get; set; }

    public string ScannedSerial { get; set; } = null!;

    public virtual InventoryAdjustment Adjustment { get; set; } = null!;

    public virtual InventoryAdjustment InventoryAdjustment { get; set; } = null!;

    public virtual StocktakeObservation Observation { get; set; } = null!;

    public virtual Product? Product { get; set; }

    public virtual SerialItem? SerialItem { get; set; }

    public virtual SerialItem? SerialItemNavigation { get; set; }

    public virtual StocktakeObservation StocktakeObservation { get; set; } = null!;

    public virtual StorageLocation? TargetLocation { get; set; }
}
