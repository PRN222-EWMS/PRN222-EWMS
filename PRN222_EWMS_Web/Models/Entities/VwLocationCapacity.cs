using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class VwLocationCapacity
{
    public int LocationId { get; set; }

    public int WarehouseId { get; set; }

    public string LocationName { get; set; } = null!;

    public string LocationType { get; set; } = null!;

    public string? CapacityMode { get; set; }

    public decimal? CapacityVolumeM3 { get; set; }

    public decimal? CapacityAreaM2 { get; set; }

    public decimal? UsedVolumeM3 { get; set; }

    public decimal? UsedAreaM2 { get; set; }

    public decimal? MaxWeightKg { get; set; }

    public decimal UsedWeightKg { get; set; }

    public long ItemCount { get; set; }
}

