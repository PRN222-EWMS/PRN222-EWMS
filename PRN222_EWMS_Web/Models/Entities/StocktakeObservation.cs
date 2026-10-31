using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class StocktakeObservation
{
    public long ObservationId { get; set; }

    public long StocktakeId { get; set; }

    public long? SerialItemId { get; set; }

    public int? ProductId { get; set; }

    public string ScannedSerial { get; set; } = null!;

    public int RoundNumber { get; set; }

    public int ExpectedQuantity { get; set; }

    public int ActualQuantity { get; set; }

    public string? ExpectedCondition { get; set; }

    public string? ActualCondition { get; set; }

    public int? ExpectedLocationId { get; set; }

    public int? ActualLocationId { get; set; }

    public string DifferenceType { get; set; } = null!;

    public DateTime CapturedAt { get; set; }

    public DateTime? ScannedAt { get; set; }

    public int? ScannedByUserId { get; set; }

    public string ReviewStatus { get; set; } = null!;

    public string? Note { get; set; }

    public virtual StorageLocation? ActualLocation { get; set; }

    public virtual StorageLocation? ExpectedLocation { get; set; }

    public virtual ICollection<InventoryAdjustmentLine> InventoryAdjustmentLineObservations { get; set; } = new List<InventoryAdjustmentLine>();

    public virtual ICollection<InventoryAdjustmentLine> InventoryAdjustmentLineStocktakeObservations { get; set; } = new List<InventoryAdjustmentLine>();

    public virtual Product? Product { get; set; }

    public virtual User? ScannedByUser { get; set; }

    public virtual SerialItem? SerialItem { get; set; }

    public virtual Stocktake Stocktake { get; set; } = null!;

    public virtual StocktakeRound StocktakeRound { get; set; } = null!;
}

