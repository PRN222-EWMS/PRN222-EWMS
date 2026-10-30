using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models.Entities;

public partial class StocktakeAssignment
{
    public long AssignmentId { get; set; }

    public long StocktakeId { get; set; }

    public int UserId { get; set; }

    public int? LocationId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    public int WarehouseId { get; set; }

    public virtual StorageLocation? Location { get; set; }

    public virtual Stocktake Stocktake { get; set; } = null!;

    public virtual Stocktake StocktakeNavigation { get; set; } = null!;

    public virtual StorageLocation? StorageLocation { get; set; }

    public virtual User User { get; set; } = null!;
}
