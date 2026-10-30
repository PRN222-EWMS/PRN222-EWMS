using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class Reservation
{
    public long ReservationId { get; set; }

    public long SupplySourceId { get; set; }

    public long? SolineId { get; set; }

    public long? TransferLineId { get; set; }

    public long? ReplacesReservationId { get; set; }

    public int ReservedQuantity { get; set; }

    public int ConsumedQuantity { get; set; }

    public int ReleasedQuantity { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Reservation> InverseReplacesReservation { get; set; } = new List<Reservation>();

    public virtual Reservation? ReplacesReservation { get; set; }

    public virtual ICollection<ReservationConsumption> ReservationConsumptions { get; set; } = new List<ReservationConsumption>();

    public virtual SalesOrderLine? Soline { get; set; }

    public virtual SupplySource SupplySource { get; set; } = null!;

    public virtual TransferOrderLine? TransferLine { get; set; }
}
