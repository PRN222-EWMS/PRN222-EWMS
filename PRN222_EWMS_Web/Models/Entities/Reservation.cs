using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

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

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public string ItemCondition { get; set; } = null!;

    public string SupplyState { get; set; } = null!;

    public long? SalesOrderId { get; set; }

    public long? TransferOrderId { get; set; }

    public virtual ICollection<Reservation> InverseReplacesReservation { get; set; } = new List<Reservation>();

    public virtual Reservation? ReplacesReservation { get; set; }

    public virtual ICollection<ReservationConsumption> ReservationConsumptionReservation1s { get; set; } = new List<ReservationConsumption>();

    public virtual ICollection<ReservationConsumption> ReservationConsumptionReservationNavigations { get; set; } = new List<ReservationConsumption>();

    public virtual ICollection<ReservationConsumption> ReservationConsumptionReservations { get; set; } = new List<ReservationConsumption>();

    public virtual SalesOrder? SalesOrder { get; set; }

    public virtual SalesOrderLine? SalesOrderLine { get; set; }

    public virtual SalesOrderLine? Soline { get; set; }

    public virtual SupplySource SupplySource { get; set; } = null!;

    public virtual SupplySource SupplySourceNavigation { get; set; } = null!;

    public virtual TransferOrderLine? TransferLine { get; set; }

    public virtual TransferOrder? TransferOrder { get; set; }

    public virtual TransferOrderLine? TransferOrderLine { get; set; }
}

