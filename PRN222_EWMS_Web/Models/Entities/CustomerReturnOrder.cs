using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class CustomerReturnOrder
{
    public long ReturnOrderId { get; set; }

    public long SalesOrderId { get; set; }

    public int WarehouseId { get; set; }

    public int CreatedByUserId { get; set; }

    public string ReturnCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<CustomerReturnOrderLine> CustomerReturnOrderLineCustomerReturnOrders { get; set; } = new List<CustomerReturnOrderLine>();

    public virtual ICollection<CustomerReturnOrderLine> CustomerReturnOrderLineReturnOrders { get; set; } = new List<CustomerReturnOrderLine>();

    public virtual ICollection<ReturnReceipt> ReturnReceiptCustomerReturnOrders { get; set; } = new List<ReturnReceipt>();

    public virtual ICollection<ReturnReceipt> ReturnReceiptReturnOrders { get; set; } = new List<ReturnReceipt>();

    public virtual SalesOrder SalesOrder { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
