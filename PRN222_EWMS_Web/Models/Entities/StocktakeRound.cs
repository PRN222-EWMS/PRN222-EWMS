using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class StocktakeRound
{
    public long RoundId { get; set; }

    public long StocktakeId { get; set; }

    public int RoundNumber { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int StartedByUserId { get; set; }

    public int? ClosedByUserId { get; set; }

    public string? RecountReason { get; set; }

    public virtual User? ClosedByUser { get; set; }

    public virtual User StartedByUser { get; set; } = null!;

    public virtual Stocktake Stocktake { get; set; } = null!;

    public virtual ICollection<StocktakeObservation> StocktakeObservations { get; set; } = new List<StocktakeObservation>();
}
