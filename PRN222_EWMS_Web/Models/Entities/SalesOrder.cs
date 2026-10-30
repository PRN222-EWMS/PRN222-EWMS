using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class SalesOrder
{
    public long SalesOrderId { get; set; }

    public int CustomerId { get; set; }

    public int WarehouseId { get; set; }

    public int CreatedByUserId { get; set; }

    public string Socode { get; set; } = null!;

    public DateOnly OrderDate { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public string Status { get; set; } = null!;

    public bool HasShortage { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public DateOnly? RequestedDate { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    public string? CustomerNameSnapshot { get; set; }

    public string? CustomerAddressSnapshot { get; set; }

    public string? CustomerPhoneSnapshot { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<CustomerReturnOrder> CustomerReturnOrders { get; set; } = new List<CustomerReturnOrder>();

    public virtual ICollection<Outbound> Outbounds { get; set; } = new List<Outbound>();

    public virtual ICollection<OutboundItem> OutboundItems { get; set; } = new List<OutboundItem>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}
