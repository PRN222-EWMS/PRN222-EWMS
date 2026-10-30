using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class ReservationConsumption
{
    public long AllocationId { get; set; }

    public long ReservationId { get; set; }

    public int ConsumedQuantity { get; set; }

    public virtual DemandAllocation Allocation { get; set; } = null!;

    public virtual Reservation Reservation { get; set; } = null!;
}
