using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class Warehouse
{
    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = null!;

    public string WarehouseType { get; set; } = null!;

    public string Address { get; set; } = null!;

    public DateOnly? RentalStartDate { get; set; }

    public DateOnly? RentalEndDate { get; set; }

    public string Status { get; set; } = null!;

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
