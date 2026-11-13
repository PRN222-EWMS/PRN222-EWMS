using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Ceo;

public class CeoStocktakeAdjustmentListViewModel
{
    public const int PageSize = 10;
    public List<InventoryAdjustment> Items { get; set; } = [];
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; }
    public int TotalCount { get; set; }
    public Dictionary<long, int> SerialCounts { get; set; } = [];
    public Dictionary<long, int> QuantityChanges { get; set; } = [];
}

public class CeoStocktakeAdjustmentDetailsViewModel
{
    public InventoryAdjustment Request { get; set; } = null!;
    public List<InventoryAdjustmentLine> Lines { get; set; } = [];
    public List<StocktakeObservation> History { get; set; } = [];
    public List<StorageLocation> Locations { get; set; } = [];
    public List<StocktakeScope> Scopes { get; set; } = [];
    public List<StocktakeAssignment> Assignments { get; set; } = [];
    public List<StocktakeRound> Rounds { get; set; } = [];
    public List<StocktakeRecountRequest> Recounts { get; set; } = [];
    public List<StocktakeObservation> Observations { get; set; } = [];
    public List<StocktakeObservation> EffectiveResults { get; set; } = [];
    public List<StocktakeHandling> Handling { get; set; } = [];
    public List<StocktakeInvestigation> Investigations { get; set; } = [];
    public List<SerialMovement> Movements { get; set; } = [];
    public List<SerialItem> CurrentItems { get; set; } = [];
    public Dictionary<int, string> UserNames { get; set; } = [];
    public Dictionary<int, string> ProductNames { get; set; } = [];
    public Dictionary<long, string> CounterNames { get; set; } = [];
    public string Version => Convert.ToBase64String(Request.RowVersion);
    public bool CanDecide => Request.Status == "SUBMITTED" && Request.Stocktake.Status == "PENDING_APPROVAL"
        && Rounds.All(r => r.Status != "OPEN") && Recounts.All(r => r.Status is not ("REQUESTED" or "IN_PROGRESS"));
}

public class CeoStocktakeAdjustmentDecisionInput
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required, StringLength(32)] public string Version { get; set; } = "";
    [StringLength(1000)] public string? DecisionNote { get; set; }
}
