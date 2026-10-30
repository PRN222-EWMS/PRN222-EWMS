using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class StocktakeScope
{
    public long ScopeId { get; set; }

    public long StocktakeId { get; set; }

    public int LocationId { get; set; }

    public int WarehouseId { get; set; }

    public virtual StorageLocation Location { get; set; } = null!;

    public virtual Stocktake Stocktake { get; set; } = null!;

    public virtual Stocktake StocktakeNavigation { get; set; } = null!;

    public virtual StorageLocation StorageLocation { get; set; } = null!;
}
