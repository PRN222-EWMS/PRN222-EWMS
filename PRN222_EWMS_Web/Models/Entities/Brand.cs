using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class Brand
{
    public int BrandId { get; set; }

    public string BrandName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<ZoneStorageRule> ZoneStorageRules { get; set; } = new List<ZoneStorageRule>();
}
