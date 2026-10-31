namespace PRN222_EWMS_Web.Models;

public class ZoneStorageRule
{
    public int ZoneRuleId { get; set; }
    public int ZoneLocationId { get; set; }
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public StorageLocation ZoneLocation { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Brand? Brand { get; set; }
}
