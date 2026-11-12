namespace PRN222_EWMS_Web.Models;

/// <summary>A saved source choice. Only Reservation commits stock after submission.</summary>
public partial class SalesOrderSourcePlan
{
    public long SourcePlanId { get; set; }
    public long SolineId { get; set; }
    public long SupplySourceId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public virtual SalesOrderLine SalesOrderLine { get; set; } = null!;
    public virtual SupplySource SupplySource { get; set; } = null!;
}
