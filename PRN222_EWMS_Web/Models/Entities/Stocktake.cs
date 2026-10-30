using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class Stocktake
{
    public long StocktakeId { get; set; }

    public int WarehouseId { get; set; }

    public int CreatedByUserId { get; set; }

    public string StocktakeCode { get; set; } = null!;

    public string ScopeType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = new List<InventoryAdjustment>();

    public virtual ICollection<StocktakeAssignment> StocktakeAssignments { get; set; } = new List<StocktakeAssignment>();

    public virtual ICollection<StocktakeObservation> StocktakeObservations { get; set; } = new List<StocktakeObservation>();

    public virtual Warehouse Warehouse { get; set; } = null!;

    public virtual ICollection<StorageLocation> Locations { get; set; } = new List<StorageLocation>();
}
