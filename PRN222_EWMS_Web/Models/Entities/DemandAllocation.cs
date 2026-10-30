using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class DemandAllocation
{
    public long AllocationId { get; set; }

    public long SupplySourceId { get; set; }

    public long? OutboundId { get; set; }

    public long? DispatchId { get; set; }

    public int Quantity { get; set; }

    public DateTime AllocatedAt { get; set; }

    public long? SalesOrderId { get; set; }

    public long? TransferOrderId { get; set; }

    public virtual TransferDispatch? Dispatch { get; set; }

    public virtual Outbound? Outbound { get; set; }

    public virtual Outbound? OutboundNavigation { get; set; }

    public virtual ICollection<ReservationConsumption> ReservationConsumptionAllocations { get; set; } = new List<ReservationConsumption>();



    public virtual SupplySource SupplySource { get; set; } = null!;

    public virtual TransferDispatch? TransferDispatch { get; set; }
}
