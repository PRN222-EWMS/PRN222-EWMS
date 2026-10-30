using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class ReservationConsumption
{
    public long AllocationId { get; set; }

    public long ReservationId { get; set; }

    public int ConsumedQuantity { get; set; }

    public long SupplySourceId { get; set; }

    public long? SalesOrderId { get; set; }

    public long? TransferOrderId { get; set; }

    public virtual DemandAllocation Allocation { get; set; } = null!;

    public virtual DemandAllocation? DemandAllocation { get; set; }

    public virtual DemandAllocation? DemandAllocationNavigation { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual Reservation? Reservation1 { get; set; }

    public virtual Reservation? ReservationNavigation { get; set; }
}

