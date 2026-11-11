namespace PRN222_EWMS_Web.Models;

public class StocktakeRecountRequest
{
    public long RecountRequestId { get; set; }
    public long StocktakeId { get; set; }
    public long SourceRoundId { get; set; }
    public int ProductId { get; set; }
    public string ScopeKind { get; set; } = "LOCATION";
    public int? LocationId { get; set; }
    public int RequestedByUserId { get; set; }
    public int AssignedToUserId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "REQUESTED";
    public long? ResultRoundId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
