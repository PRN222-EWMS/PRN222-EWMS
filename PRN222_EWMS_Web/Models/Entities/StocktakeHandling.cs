namespace PRN222_EWMS_Web.Models;

public class StocktakeHandling
{
    public long HandlingId { get; set; }
    public long StocktakeId { get; set; }
    public long ObservationId { get; set; }
    public long SerialItemId { get; set; }
    public string ScannedSerial { get; set; } = "";
    public int ProductId { get; set; }
    public int? FoundLocationId { get; set; }
    public int? OriginalLocationId { get; set; }
    public int? TargetLocationId { get; set; }
    public string ActualCondition { get; set; } = "GOOD";
    public string Status { get; set; } = "WAITING";
    public int RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? MovedByUserId { get; set; }
    public DateTime? MovedAt { get; set; }
    public long? MovementId { get; set; }
    public string? Note { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[] RowVersion { get; set; } = [];
}
