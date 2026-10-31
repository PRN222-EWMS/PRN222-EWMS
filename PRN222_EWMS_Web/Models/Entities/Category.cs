using System;
using System.Collections.Generic;

namespace PRN222_EWMS_Web.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<ZoneStorageRule> ZoneStorageRules { get; set; } = new List<ZoneStorageRule>();
}

