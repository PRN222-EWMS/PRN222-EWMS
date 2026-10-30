using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class StorageLocation
{
    public int LocationId { get; set; }

    public int WarehouseId { get; set; }

    public int? ParentLocationId { get; set; }

    public string LocationName { get; set; } = null!;

    public string LocationType { get; set; } = null!;

    public string? ZoneType { get; set; }

    public string? StorageType { get; set; }

    public string? ConditionPolicy { get; set; }

    public string? CapacityMode { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? HeightCm { get; set; }

    public decimal? MaxWeightKg { get; set; }

    public string Status { get; set; } = null!;

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<InventoryAdjustmentLine> InventoryAdjustmentLines { get; set; } = new List<InventoryAdjustmentLine>();

    public virtual ICollection<StorageLocation> InverseParentLocation { get; set; } = new List<StorageLocation>();

    public virtual StorageLocation? ParentLocation { get; set; }

    public virtual ICollection<SerialItem> SerialItems { get; set; } = new List<SerialItem>();

    public virtual ICollection<SerialMovement> SerialMovementFromLocations { get; set; } = new List<SerialMovement>();

    public virtual ICollection<SerialMovement> SerialMovementToLocations { get; set; } = new List<SerialMovement>();

    public virtual ICollection<StocktakeAssignment> StocktakeAssignments { get; set; } = new List<StocktakeAssignment>();

    public virtual ICollection<StocktakeObservation> StocktakeObservationActualLocations { get; set; } = new List<StocktakeObservation>();

    public virtual ICollection<StocktakeObservation> StocktakeObservationExpectedLocations { get; set; } = new List<StocktakeObservation>();

    public virtual Warehouse Warehouse { get; set; } = null!;

    public virtual ICollection<ZoneStorageRule> ZoneStorageRules { get; set; } = new List<ZoneStorageRule>();

    public virtual ICollection<Stocktake> Stocktakes { get; set; } = new List<Stocktake>();
}
