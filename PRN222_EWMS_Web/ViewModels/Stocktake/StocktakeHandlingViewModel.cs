using System.ComponentModel.DataAnnotations;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Putaway;

namespace PRN222_EWMS_Web.ViewModels.Stocktaking;

public class StocktakeHandlingViewModel
{
    public Models.Stocktake Plan { get; set; } = null!;
    public StocktakeHandling Handling { get; set; } = null!;
    public List<StorageLocation> Locations { get; set; } = [];
    public List<PutawayLocationViewModel> Suggestions { get; set; } = [];
    public string Version { get; set; } = "";
    public string HandlingVersion { get; set; } = "";
    public string ProductName { get; set; } = "";
    public bool CanMove { get; set; }
    public string? Message { get; set; }
    public long? RecountRequestId { get; set; }
}

public class StocktakeMoveInput : StocktakeActionInput
{
    public long HandlingId { get; set; }
    [Required] public string HandlingVersion { get; set; } = "";
    public int? TargetLocationId { get; set; }
    public bool Confirmed { get; set; }
}
