namespace PRN222_EWMS_Web.Services.Warehouse;

public sealed record CapacityProduct(int Id, string Code, int CategoryId, int BrandId,
    decimal Length, decimal Width, decimal Height, decimal Weight);
public sealed record CapacityLoad(CapacityProduct Product, string Condition, long Quantity);
public sealed class CapacitySlot
{
    public int Id { get; init; }
    public bool Reserve { get; init; }
    public bool Floor { get; init; }
    public string Condition { get; init; } = "";
    public decimal Length { get; init; }
    public decimal Width { get; init; }
    public decimal? Height { get; init; }
    public decimal? MaxWeight { get; init; }
    public List<(int CategoryId, int? BrandId)> Rules { get; init; } = [];
    public List<CapacityLoad> Loads { get; init; } = [];
    public bool Accepts(CapacityLoad load) => Condition == load.Condition &&
        (Reserve || Rules.Any(r => r.CategoryId == load.Product.CategoryId && (r.BrandId == null || r.BrandId == load.Product.BrandId)));
}

// One shared allocation for every SKU, including arrivals and awaiting putaway.
// Floor packing follows the existing putaway rule: fixed orientation, no stacking.
public static class TransferCapacityPlanner
{
    public static List<string> Allocate(List<CapacitySlot> slots, IEnumerable<CapacityLoad> demands)
    {
        var errors = new List<string>();
        foreach (var load in demands.Where(l => l.Quantity > 0)
            .OrderBy(l => slots.Count(s => s.Accepts(l))).ThenByDescending(l => l.Product.Length * l.Product.Width * l.Product.Height)
            .ThenBy(l => l.Product.Id))
        {
            long remaining = load.Quantity;
            if (load.Product.Length <= 0 || load.Product.Width <= 0 || load.Product.Height <= 0 || load.Product.Weight <= 0)
            {
                errors.Add($"{load.Product.Code}: thiếu kích thước hoặc khối lượng hợp lệ.");
                continue;
            }
            foreach (var slot in slots.Where(s => s.Accepts(load)).OrderBy(s => s.Reserve).ThenBy(s => s.Id))
            {
                long low = 0, high = remaining;
                while (low < high)
                {
                    long mid = low + (high - low + 1) / 2;
                    if (Fits(slot, load with { Quantity = mid })) low = mid; else high = mid - 1;
                }
                if (low > 0) slot.Loads.Add(load with { Quantity = low });
                remaining -= low;
                if (remaining == 0) break;
            }
            if (remaining > 0) errors.Add($"{load.Product.Code}: còn thiếu chỗ cho {remaining:N0}/{load.Quantity:N0} sản phẩm ({load.Condition}).");
        }
        return errors;
    }

    public static bool Fits(CapacitySlot slot, CapacityLoad incoming)
    {
        var all = slot.Loads.Append(incoming).Where(l => l.Quantity > 0).ToList();
        if (all.Any(l => l.Condition != slot.Condition || l.Product.Length <= 0 || l.Product.Width <= 0 || l.Product.Height <= 0
            || l.Product.Weight <= 0 || l.Product.Length > slot.Length || l.Product.Width > slot.Width
            || (slot.Height.HasValue && l.Product.Height > slot.Height.Value))) return false;
        if (slot.MaxWeight.HasValue && all.Sum(l => l.Quantity * l.Product.Weight) > slot.MaxWeight.Value) return false;
        if (!slot.Floor) return slot.Height > 0 && all.Sum(l => l.Quantity * l.Product.Length * l.Product.Width * l.Product.Height)
            <= slot.Length * slot.Width * slot.Height.Value;
        return CanPackFloor(slot.Length, slot.Width, all);
    }

    private static bool CanPackFloor(decimal length, decimal width, IEnumerable<CapacityLoad> loads)
    {
        // Full equal rows are counted in bulk, so a large quantity cannot cause a million-iteration loop.
        var partialRows = new List<(decimal Width, decimal UsedLength)>();
        decimal usedWidth = 0;
        foreach (var load in loads.OrderByDescending(l => l.Product.Width).ThenByDescending(l => l.Product.Length))
        {
            var p = load.Product;
            long left = load.Quantity;
            for (int i = 0; i < partialRows.Count && left > 0; i++)
            {
                var row = partialRows[i];
                if (p.Width > row.Width) continue;
                long count = Math.Min(left, (long)decimal.Floor((length - row.UsedLength) / p.Length));
                partialRows[i] = (row.Width, row.UsedLength + count * p.Length);
                left -= count;
            }
            if (left == 0) continue;
            long perRow = (long)decimal.Floor(length / p.Length);
            if (perRow == 0) return false;
            long rows = (left + perRow - 1) / perRow;
            usedWidth += rows * p.Width;
            if (usedWidth > width) return false;
            // Full rows can have a residual strip which smaller products can use.
            // Keeping only the last row is conservative and prevents overstating capacity.
            long lastCount = left - (rows - 1) * perRow;
            partialRows.Add((p.Width, lastCount * p.Length));
        }
        return true;
    }
}
