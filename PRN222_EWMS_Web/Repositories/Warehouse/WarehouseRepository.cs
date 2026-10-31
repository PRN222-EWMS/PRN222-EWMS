using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Warehouse;

public class WarehouseRepository(EwmsDbContext db) : IWarehouseRepository
{
    public async Task<T> InTransactionAsync<T>(int warehouseId, Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Serialize structural changes in this warehouse. Serializable reads also retain stock/scope locks until commit.
        await db.Database.SqlQuery<int>($"""
            SELECT WarehouseID AS [Value]
            FROM dbo.Warehouse WITH (UPDLOCK, HOLDLOCK)
            WHERE WarehouseID = {warehouseId}
            """).ToListAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> HasStockAsync(int warehouseId, IReadOnlyCollection<int>? locationIds,
        CancellationToken cancellationToken = default)
    {
        if (locationIds is null)
        {
            // Include awaiting putaway and fail closed if a serial's warehouse and location disagree.
            return await db.Database.SqlQuery<int>($"""
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.SerialItem AS s
                    WHERE s.StockState = 'IN_WAREHOUSE'
                        AND (s.CurrentWarehouseID = {warehouseId} OR EXISTS (
                            SELECT 1 FROM dbo.StorageLocation AS location
                            WHERE location.LocationID = s.CurrentLocationID AND location.WarehouseID = {warehouseId}
                        ))
                ) THEN 1 ELSE 0 END AS [Value]
                """).SingleAsync(cancellationToken) == 1;
        }
        if (locationIds.Count == 0) return false;
        var idsJson = JsonSerializer.Serialize(locationIds);
        return await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.SerialItem AS s
                INNER JOIN dbo.StorageLocation AS location ON location.LocationID = s.CurrentLocationID
                WHERE location.WarehouseID = {warehouseId} AND s.StockState = 'IN_WAREHOUSE'
                    AND s.CurrentLocationID IN (
                        SELECT LocationID FROM OPENJSON({idsJson}) WITH (LocationID int '$')
                    )
            ) THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(cancellationToken) == 1;
    }

    public Task<List<int?>> GetLockedScopeRootsAsync(int warehouseId, CancellationToken cancellationToken = default) =>
        db.Database.SqlQuery<int?>($"""
            SELECT DISTINCT CASE WHEN s.ScopeType = 'WHOLE_WAREHOUSE' THEN CAST(NULL AS int)
                ELSE location.LocationID END AS [Value]
            FROM dbo.Stocktake AS s
            LEFT JOIN dbo.StocktakeScope AS scope ON scope.StocktakeID = s.StocktakeID
            LEFT JOIN dbo.StorageLocation AS location ON location.LocationID = scope.LocationID
                AND location.WarehouseID = s.WarehouseID
            WHERE s.WarehouseID = {warehouseId}
                AND s.Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
            """).ToListAsync(cancellationToken);

    public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        db.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ThenBy(c => c.CategoryId).ToListAsync(cancellationToken);

    public Task<List<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        db.Brands.AsNoTracking().OrderBy(b => b.BrandName).ThenBy(b => b.BrandId).ToListAsync(cancellationToken);

    public Task<bool> LocationNameExistsAsync(int warehouseId, int? parentId, string name, int? excludingId,
        CancellationToken cancellationToken = default) =>
        db.StorageLocations.AnyAsync(l => l.WarehouseId == warehouseId && l.ParentLocationId == parentId
            && l.LocationName == name && (!excludingId.HasValue || l.LocationId != excludingId), cancellationToken);

    public async Task SaveLocationAsync(StorageLocation location, byte[]? originalRowVersion,
        IReadOnlyList<ZoneStorageRule> rules, IReadOnlyCollection<int> deactivateIds,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Storage configuration must be saved in a transaction.");

        if (location.LocationId == 0)
        {
            db.StorageLocations.Add(location);
        }
        else
        {
            if (originalRowVersion is not { Length: 8 })
                throw new ArgumentException("A valid location RowVersion is required.", nameof(originalRowVersion));
            var current = await db.StorageLocations.SingleOrDefaultAsync(
                l => l.LocationId == location.LocationId && l.WarehouseId == location.WarehouseId, cancellationToken)
                ?? throw new DbUpdateConcurrencyException("Storage location no longer exists.");
            if (current.ParentLocationId != location.ParentLocationId || current.LocationType != location.LocationType)
                throw new InvalidOperationException("Existing storage locations cannot be reparented or change hierarchy level.");

            var entry = db.Entry(current);
            entry.CurrentValues.SetValues(location);
            entry.Property(l => l.RowVersion).OriginalValue = originalRowVersion;
            // Even a rule-only edit must check and advance the Zone's RowVersion.
            entry.Property(l => l.LocationName).IsModified = true;
        }
        await db.SaveChangesAsync(cancellationToken);

        if (location.LocationType == "ZONE")
        {
            var oldRules = await db.ZoneStorageRules.Where(r => r.ZoneLocationId == location.LocationId
                    && r.ZoneLocation.WarehouseId == location.WarehouseId)
                .ToListAsync(cancellationToken);
            db.ZoneStorageRules.RemoveRange(oldRules);
            // Delete before inserting replacement pairs to respect the unique zone/category/brand index.
            await db.SaveChangesAsync(cancellationToken);
            foreach (var rule in rules)
                db.ZoneStorageRules.Add(new ZoneStorageRule
                {
                    ZoneLocationId = location.LocationId, CategoryId = rule.CategoryId, BrandId = rule.BrandId
                });
        }

        if (deactivateIds.Count > 0)
        {
            var ids = deactivateIds.ToArray();
            var descendants = await db.StorageLocations.Where(l => l.WarehouseId == location.WarehouseId
                && l.LocationId != location.LocationId && ids.Contains(l.LocationId)).ToListAsync(cancellationToken);
            foreach (var descendant in descendants) descendant.Status = "INACTIVE";
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<WarehouseEntity?> FindByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.WarehouseId == id, cancellationToken);

    public Task<List<StorageLocation>> GetLocationsAsync(int warehouseId, CancellationToken cancellationToken = default) =>
        db.StorageLocations.AsNoTracking().Where(l => l.WarehouseId == warehouseId)
            .OrderBy(l => l.LocationName).ThenBy(l => l.LocationId).ToListAsync(cancellationToken);

    public Task<List<ZoneStorageRule>> GetZoneRulesAsync(int warehouseId, CancellationToken cancellationToken = default) =>
        db.ZoneStorageRules.AsNoTracking().Where(r => r.ZoneLocation.WarehouseId == warehouseId)
            .Include(r => r.Category).Include(r => r.Brand)
            .OrderBy(r => r.Category.CategoryName).ThenBy(r => r.Brand!.BrandName).ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken = default) =>
        db.Warehouses.AnyAsync(w => w.WarehouseName == name && (!excludingId.HasValue || w.WarehouseId != excludingId), cancellationToken);

    public async Task AddAsync(WarehouseEntity warehouse, CancellationToken cancellationToken = default)
    {
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WarehouseEntity warehouse, byte[] originalRowVersion, CancellationToken cancellationToken = default)
    {
        // Cookie validation may already have loaded the user's assigned warehouse in this scope.
        var current = await db.Warehouses.FindAsync([warehouse.WarehouseId], cancellationToken)
            ?? throw new DbUpdateConcurrencyException("Warehouse no longer exists.");
        var entry = db.Entry(current);
        entry.CurrentValues.SetValues(warehouse);
        entry.Property(w => w.WarehouseName).IsModified = true;
        entry.Property(w => w.WarehouseType).IsModified = true;
        entry.Property(w => w.Address).IsModified = true;
        entry.Property(w => w.Status).IsModified = true;
        entry.Property(w => w.RentalStartDate).IsModified = true;
        entry.Property(w => w.RentalEndDate).IsModified = true;
        entry.Property(w => w.RowVersion).OriginalValue = originalRowVersion;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WarehouseEntity> Items, int TotalCount, int TotalWarehouses, int Page)> GetPageAsync(
        string? search, string? warehouseType, string? status,
        DateOnly? rentalFrom, DateOnly? rentalTo, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        var query = db.Warehouses.AsNoTracking();
        var totalWarehouses = await query.CountAsync(cancellationToken);
        var hasFilters = search is not null || warehouseType is not null || status is not null
            || rentalFrom.HasValue || rentalTo.HasValue;

        if (search is not null)
        {
            if (int.TryParse(search, out var id))
                query = query.Where(w => w.WarehouseName.Contains(search) || w.WarehouseId == id);
            else
                query = query.Where(w => w.WarehouseName.Contains(search));
        }
        if (warehouseType is not null) query = query.Where(w => w.WarehouseType == warehouseType);
        if (status is not null) query = query.Where(w => w.Status == status);

        // Inclusive overlap with a known rental period; MAIN warehouses have no rental period.
        if (rentalFrom.HasValue || rentalTo.HasValue)
        {
            query = query.Where(w => w.WarehouseType == "RENTED"
                && w.RentalStartDate.HasValue && w.RentalEndDate.HasValue);
            if (rentalFrom.HasValue) query = query.Where(w => w.RentalEndDate >= rentalFrom.Value);
            if (rentalTo.HasValue) query = query.Where(w => w.RentalStartDate <= rentalTo.Value);
        }

        var totalCount = hasFilters ? await query.CountAsync(cancellationToken) : totalWarehouses;
        var totalPages = totalCount == 0 ? 1 : (totalCount - 1) / pageSize + 1;
        page = Math.Clamp(page, 1, totalPages);
        var items = await query.OrderBy(w => w.WarehouseName).ThenBy(w => w.WarehouseId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return (items, totalCount, totalWarehouses, page);
    }
}
