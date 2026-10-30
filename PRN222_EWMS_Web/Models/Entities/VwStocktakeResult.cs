using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class VwStocktakeResult
{
    public long StocktakeId { get; set; }

    public string StocktakeCode { get; set; } = null!;

    public int WarehouseId { get; set; }

    public string StocktakeStatus { get; set; } = null!;

    public int RoundNumber { get; set; }

    public string RoundStatus { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public long ObservationId { get; set; }

    public string ScannedSerial { get; set; } = null!;

    public int? ProductId { get; set; }

    public int ExpectedQuantity { get; set; }

    public int ActualQuantity { get; set; }

    public string? ExpectedCondition { get; set; }

    public string? ActualCondition { get; set; }

    public int? ExpectedLocationId { get; set; }

    public int? ActualLocationId { get; set; }

    public string DifferenceType { get; set; } = null!;

    public string ReviewStatus { get; set; } = null!;

    public int? ScannedByUserId { get; set; }

    public DateTime? ScannedAt { get; set; }

    public string? Note { get; set; }
}
