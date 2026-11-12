using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Stocktaking;

public class StocktakeReviewViewModel
{
    public Models.Stocktake Plan { get; set; } = null!;
    public List<StocktakeAssignment> Assignments { get; set; } = [];
    public List<StocktakeRecountRequest> Requests { get; set; } = [];
    public List<StocktakeRound> Rounds { get; set; } = [];
    public List<StocktakeObservation> Observations { get; set; } = [];
    public List<StocktakeObservation> History { get; set; } = [];
    public List<StocktakeHandling> Handling { get; set; } = [];
    public List<StorageLocation> Locations { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<User> Staff { get; set; } = [];
    public List<InventoryAdjustment> Adjustments { get; set; } = [];
    public string Version { get; set; } = "";
    public List<StocktakeRecountChoice> RecountChoices { get; set; } = [];
    public List<StocktakeInvestigation> Investigations { get; set; } = [];
    public StocktakeInvestigation? InvestigationContext { get; set; }
    public HashSet<long> SearchableShortageIds { get; set; } = [];
    public HashSet<long> ConfirmedShortageIds { get; set; } = [];
    public List<AuditLog> NoSurplusDecisions { get; set; } = [];
    public List<AuditLog> SourceDecisions { get; set; } = [];
    public Dictionary<long, List<StocktakeSourceReceipt>> CompletedSources { get; set; } = [];
    public HashSet<long> NoSurplusProofIds { get; set; } = [];
}

public class StocktakeSourceResolutionInput : StocktakeActionInput
{
    [Range(1, long.MaxValue)] public long ObservationId { get; set; }
    [Required, StringLength(40)] public string Source { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public class StocktakeNoSurplusInput : StocktakeActionInput
{
    [Range(1, long.MaxValue)] public long ObservationId { get; set; }
    public long InvestigationId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public class StocktakeInvestigationInput : StocktakeActionInput
{
    public long ObservationId { get; set; }
    public List<long> ShortageObservationIds { get; set; } = [];
    [Range(1, int.MaxValue)] public int AssignedToUserId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public class StocktakeInvestigationConclusionInput : StocktakeActionInput
{
    [Required] public string Outcome { get; set; } = "";
    [Required, StringLength(1000)] public string Conclusion { get; set; } = "";
}

public record StocktakeRecountChoice(int ProductId, string ScopeKind, int? LocationId, List<int> StaffIds);

public class StocktakeRecountInput : StocktakeActionInput
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Required] public string ScopeKind { get; set; } = "LOCATION";
    public int? LocationId { get; set; }
    [Range(1, int.MaxValue)] public int AssignedToUserId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public class StocktakeAdjustmentInput : StocktakeActionInput
{
    public List<long> ObservationIds { get; set; } = [];
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
