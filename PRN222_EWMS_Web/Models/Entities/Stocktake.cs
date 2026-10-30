using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

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

    public bool IncludeAwaitingPutaway { get; set; }

    public DateTime? PlannedAt { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<InventoryAdjustment> InventoryAdjustments { get; set; } = new List<InventoryAdjustment>();

    public virtual ICollection<StocktakeAssignment> StocktakeAssignmentStocktakeNavigations { get; set; } = new List<StocktakeAssignment>();

    public virtual ICollection<StocktakeAssignment> StocktakeAssignmentStocktakes { get; set; } = new List<StocktakeAssignment>();

    public virtual ICollection<StocktakeObservation> StocktakeObservations { get; set; } = new List<StocktakeObservation>();

    public virtual StocktakeRound? StocktakeRound { get; set; }

    public virtual ICollection<StocktakeScope> StocktakeScopeStocktakeNavigations { get; set; } = new List<StocktakeScope>();

    public virtual ICollection<StocktakeScope> StocktakeScopeStocktakes { get; set; } = new List<StocktakeScope>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}

