USE [WMSDB];
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Run after stocktake-test-main-warehouse.sql, before starting a new count.
-- Place test serials 001 and 002. Serial 003 stays awaiting putaway.
DECLARE @WarehouseID int, @StaffID int, @TargetID int, @ProductID int;
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Items TABLE (SerialItemID bigint PRIMARY KEY);
BEGIN TRY
    BEGIN TRANSACTION;
    SELECT @WarehouseID = WarehouseID
    FROM dbo.Warehouse WITH (UPDLOCK, HOLDLOCK)
    WHERE WarehouseName = N'Kho chính EWMS' AND WarehouseType = 'MAIN' AND Status = 'ACTIVE';
    IF @@ROWCOUNT <> 1
        THROW 51601, N'Không xác định được Kho chính EWMS đang hoạt động.', 1;

    SELECT @ProductID = MIN(ProductID)
    FROM dbo.SerialItem WITH (UPDLOCK, HOLDLOCK)
    WHERE SerialNumber IN ('KKMAIN-TEST01-001', 'KKMAIN-TEST01-002')
      AND CurrentWarehouseID = @WarehouseID AND StockState = 'IN_WAREHOUSE'
      AND ItemCondition = 'GOOD' AND IsQuarantined = 0 AND CurrentLocationID IS NULL
    HAVING COUNT(*) = 2 AND COUNT(DISTINCT ProductID) = 1;
    IF @ProductID IS NULL
        THROW 51602, N'Cần hai serial test 001 và 002 còn chờ xếp trong kho chính. Chạy file thêm hàng trước hoặc kiểm tra xem đã xếp rồi chưa.', 1;

    SELECT TOP (1) @StaffID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND u.WarehouseID = @WarehouseID AND r.RoleName = 'WAREHOUSE_STAFF'
    ORDER BY u.UserID;
    IF @StaffID IS NULL THROW 51603, N'Kho chính chưa có nhân viên kho đang hoạt động.', 1;

    INSERT @Items
    SELECT SerialItemID FROM dbo.SerialItem
    WHERE SerialNumber IN ('KKMAIN-TEST01-001', 'KKMAIN-TEST01-002');
    IF EXISTS (SELECT 1 FROM dbo.PickingClaim pc JOIN @Items t ON t.SerialItemID = pc.SerialItemID)
        THROW 51604, N'Serial test đang được giữ cho picking. Không thể xếp bộ test này.', 1;

    -- Build the locked location set, including descendants of each scope root.
    DECLARE @Locked TABLE (LocationID int PRIMARY KEY);
    ;WITH LockedTree AS (
        SELECT sc.LocationID
        FROM dbo.StocktakeScope sc
        JOIN dbo.Stocktake st ON st.StocktakeID = sc.StocktakeID
        WHERE st.WarehouseID = @WarehouseID
          AND st.Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
        UNION ALL
        SELECT l.LocationID FROM dbo.StorageLocation l JOIN LockedTree p ON l.ParentLocationID = p.LocationID
        WHERE l.WarehouseID = @WarehouseID
    )
    INSERT @Locked SELECT DISTINCT LocationID FROM LockedTree OPTION (MAXRECURSION 100);

    IF EXISTS (
        SELECT 1 FROM dbo.Stocktake
        WHERE WarehouseID = @WarehouseID
          AND Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
          AND (ScopeType = 'WHOLE_WAREHOUSE' OR IncludeAwaitingPutaway = 1)
    )
        THROW 51605, N'Hàng chờ xếp đang thuộc đợt kiểm kê bị khóa. Cần xử lý đợt hiện tại trước khi xếp bộ test.', 1;

    -- Use an existing empty GOOD tier with enough room for both test items.
    SELECT TOP (1) @TargetID = t.LocationID
    FROM dbo.StorageLocation t WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.StorageLocation rack ON rack.LocationID = t.ParentLocationID
    JOIN dbo.StorageLocation rowloc ON rowloc.LocationID = rack.ParentLocationID
    JOIN dbo.StorageLocation zone ON zone.LocationID = rowloc.ParentLocationID
    JOIN dbo.Product p ON p.ProductID = @ProductID
    WHERE t.WarehouseID = @WarehouseID AND rack.WarehouseID = @WarehouseID
      AND rowloc.WarehouseID = @WarehouseID AND zone.WarehouseID = @WarehouseID
      AND t.Status = 'ACTIVE' AND rack.Status = 'ACTIVE'
      AND rowloc.Status = 'ACTIVE' AND zone.Status = 'ACTIVE'
      AND t.LocationType = 'TIER' AND rack.LocationType = 'RACK'
      AND rowloc.LocationType = 'ROW' AND rowloc.StorageType = 'RACK'
      AND zone.LocationType = 'ZONE' AND zone.ParentLocationID IS NULL
      AND zone.ZoneType IN ('STANDARD', 'RESERVE') AND rack.ConditionPolicy = 'GOOD'
      AND (zone.ZoneType = 'RESERVE' OR EXISTS (
          SELECT 1 FROM dbo.ZoneStorageRule zr WHERE zr.ZoneLocationID = zone.LocationID
          AND zr.CategoryID = p.CategoryID AND (zr.BrandID IS NULL OR zr.BrandID = p.BrandID)
      ))
      AND p.Status = 'ACTIVE' AND p.LengthCm > 0 AND p.WidthCm > 0
      AND p.HeightCm > 0 AND p.GrossWeightKg > 0
      AND t.CapacityMode = 'VOLUME'
      AND t.LengthCm >= p.LengthCm AND t.WidthCm >= p.WidthCm AND t.HeightCm >= p.HeightCm
      AND t.LengthCm * t.WidthCm * t.HeightCm >= 2 * p.LengthCm * p.WidthCm * p.HeightCm
      AND (t.MaxWeightKg IS NULL OR t.MaxWeightKg >= 2 * p.GrossWeightKg)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.SerialItem s WHERE s.CurrentLocationID = t.LocationID AND s.StockState = 'IN_WAREHOUSE'
      )
      AND NOT EXISTS (
          SELECT 1 FROM @Locked l WHERE l.LocationID IN (t.LocationID, rack.LocationID, rowloc.LocationID, zone.LocationID)
      )
    ORDER BY CASE WHEN zone.ZoneType = 'STANDARD' THEN 0 ELSE 1 END, t.LocationID;
    IF @TargetID IS NULL
        THROW 51606, N'Chưa có tầng kệ GOOD trống, hợp loại/hãng, đủ sức chứa và không bị khóa kiểm kê. Cần cấu hình vị trí hoặc dùng Putaway để xem nguyên nhân chi tiết.', 1;

    INSERT dbo.SerialMovement (
        SerialItemID, PerformedByUserID, EventType, OccurredAt,
        FromState, ToState, FromCondition, ToCondition, FromWarehouseID, ToWarehouseID,
        FromLocationID, ToLocationID, FromQuarantined, ToQuarantined, InboundID, Note
    )
    SELECT s.SerialItemID, @StaffID, 'PUTAWAY', @Now,
        s.StockState, s.StockState, s.ItemCondition, s.ItemCondition, @WarehouseID, @WarehouseID,
        NULL, @TargetID, s.IsQuarantined, s.IsQuarantined, NULL, N'Xếp dữ liệu test KKMAIN-TEST01 trước khi kiểm kê'
    FROM dbo.SerialItem s JOIN @Items t ON t.SerialItemID = s.SerialItemID;
    UPDATE s SET CurrentLocationID = @TargetID
    FROM dbo.SerialItem s JOIN @Items t ON t.SerialItemID = s.SerialItemID;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT s.SerialNumber, p.ProductName, w.WarehouseName,
    zone.LocationName AS Khu, rowloc.LocationName AS Day,
    rack.LocationName AS Ke, t.LocationName AS Tang,
    s.CurrentLocationID, s.ItemCondition
FROM dbo.SerialItem s
JOIN dbo.Product p ON p.ProductID = s.ProductID
JOIN dbo.Warehouse w ON w.WarehouseID = s.CurrentWarehouseID
LEFT JOIN dbo.StorageLocation t ON t.LocationID = s.CurrentLocationID
LEFT JOIN dbo.StorageLocation rack ON rack.LocationID = t.ParentLocationID
LEFT JOIN dbo.StorageLocation rowloc ON rowloc.LocationID = rack.ParentLocationID
LEFT JOIN dbo.StorageLocation zone ON zone.LocationID = rowloc.ParentLocationID
WHERE s.SerialNumber IN ('KKMAIN-TEST01-001', 'KKMAIN-TEST01-002', 'KKMAIN-TEST01-003')
ORDER BY s.SerialNumber;
