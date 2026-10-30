using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class VwSalesOrderProgress
{
    public long SalesOrderId { get; set; }

    public string Socode { get; set; } = null!;

    public int CustomerId { get; set; }

    public int WarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? RequestedDate { get; set; }

    public bool HasShortage { get; set; }

    public long SolineId { get; set; }

    public int ProductId { get; set; }

    public int RequestedQuantity { get; set; }

    public int CancelledQuantity { get; set; }

    public long IssuedQuantity { get; set; }

    public long? RemainingQuantity { get; set; }

    public long ReservedBalance { get; set; }
}
