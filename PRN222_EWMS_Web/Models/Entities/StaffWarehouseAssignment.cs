using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class StaffWarehouseAssignment
{
    public long AssignmentHistoryId { get; set; }

    public int UserId { get; set; }

    public int WarehouseId { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
