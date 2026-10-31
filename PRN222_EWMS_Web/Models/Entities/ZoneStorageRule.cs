using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class ZoneStorageRule
{
    public int ZoneRuleId { get; set; }

    public int ZoneLocationId { get; set; }

    public int CategoryId { get; set; }

    public int? BrandId { get; set; }

    public virtual Brand? Brand { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual StorageLocation ZoneLocation { get; set; } = null!;
}
