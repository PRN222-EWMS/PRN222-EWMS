using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class InventoryAdjustment
{
    public long AdjustmentId { get; set; }

    public long StocktakeId { get; set; }

    public int SubmittedByUserId { get; set; }

    public int? DecidedByUserId { get; set; }

    public string AdjustmentCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime? AppliedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? DecidedByUser { get; set; }

    public virtual ICollection<InventoryAdjustmentLine> InventoryAdjustmentLineAdjustments { get; set; } = new List<InventoryAdjustmentLine>();

    public virtual ICollection<InventoryAdjustmentLine> InventoryAdjustmentLineInventoryAdjustments { get; set; } = new List<InventoryAdjustmentLine>();

    public virtual ICollection<SerialItem> SerialItems { get; set; } = new List<SerialItem>();

    public virtual ICollection<SerialMovement> SerialMovements { get; set; } = new List<SerialMovement>();

    public virtual Stocktake Stocktake { get; set; } = null!;

    public virtual User SubmittedByUser { get; set; } = null!;

    public virtual ICollection<SupplySource> SupplySources { get; set; } = new List<SupplySource>();
}
