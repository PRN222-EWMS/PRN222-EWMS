namespace PRN222_EWMS_Web.Models;

/// <summary>Links an unknown-serial investigation to a separate whole-warehouse count; no stock is posted by this link.</summary>
public class StocktakeInvestigation
{
    public long StocktakeInvestigationId { get; set; }
    public long StocktakeId { get; set; }
    public long ObservationId { get; set; }
    public List<long> ShortageObservationIds { get; set; } = [];
    public List<long> StillMissingObservationIds { get; set; } = [];
    public long InvestigationStocktakeId { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? Outcome { get; set; }
    public string? Conclusion { get; set; }
}
