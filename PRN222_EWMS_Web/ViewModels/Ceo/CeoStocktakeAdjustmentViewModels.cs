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
}

public class CeoStocktakeAdjustmentDetailsViewModel
{
    public InventoryAdjustment Request { get; set; } = null!;
    public List<InventoryAdjustmentLine> Lines { get; set; } = [];
    public List<StocktakeObservation> History { get; set; } = [];
    public List<StorageLocation> Locations { get; set; } = [];
    public string Version => Convert.ToBase64String(Request.RowVersion);
    public bool CanDecide => Request.Status == "SUBMITTED";
}

public class CeoStocktakeAdjustmentDecisionInput
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required, StringLength(32)] public string Version { get; set; } = "";
    [StringLength(1000)] public string? DecisionNote { get; set; }
}
