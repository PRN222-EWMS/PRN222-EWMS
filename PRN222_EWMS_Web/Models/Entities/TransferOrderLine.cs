using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class TransferOrderLine
{
    public long TransferLineId { get; set; }

    public long TransferOrderId { get; set; }

    public int ProductId { get; set; }

    public int RequestedQuantity { get; set; }

    public int CancelledQuantity { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<Reservation> ReservationTransferLines { get; set; } = new List<Reservation>();

    public virtual ICollection<Reservation> ReservationTransferOrderLines { get; set; } = new List<Reservation>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItemTransferLines { get; set; } = new List<TransferDispatchItem>();

    public virtual ICollection<TransferDispatchItem> TransferDispatchItemTransferOrderLines { get; set; } = new List<TransferDispatchItem>();

    public virtual TransferOrder TransferOrder { get; set; } = null!;
}

