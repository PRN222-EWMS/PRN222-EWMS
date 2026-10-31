using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class SalesOrderLine
{
    public long SolineId { get; set; }

    public long SalesOrderId { get; set; }

    public int ProductId { get; set; }

    public int RequestedQuantity { get; set; }

    public int CancelledQuantity { get; set; }

    public virtual ICollection<CustomerReturnOrderLine> CustomerReturnOrderLineSalesOrderLines { get; set; } = new List<CustomerReturnOrderLine>();

    public virtual ICollection<CustomerReturnOrderLine> CustomerReturnOrderLineSolines { get; set; } = new List<CustomerReturnOrderLine>();

    public virtual ICollection<OutboundItem> OutboundItemSalesOrderLines { get; set; } = new List<OutboundItem>();

    public virtual ICollection<OutboundItem> OutboundItemSolines { get; set; } = new List<OutboundItem>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<Reservation> ReservationSalesOrderLines { get; set; } = new List<Reservation>();

    public virtual ICollection<Reservation> ReservationSolines { get; set; } = new List<Reservation>();

    public virtual SalesOrder SalesOrder { get; set; } = null!;
}

