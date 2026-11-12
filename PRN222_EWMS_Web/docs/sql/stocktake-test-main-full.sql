-- Dữ liệu test kiểm kê. Chạy trước khi bắt đầu đợt kiểm kê mới.
-- Đổi @Tag để tạo một bộ khác sau khi đã dùng bộ hiện tại.
USE [WMSDB];
SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Hãy mở cửa sổ truy vấn mới để chạy bộ dữ liệu test.', 16, 1);
    RETURN;
END;

SET XACT_ABORT ON;

DECLARE @WarehouseID int, @StaffID int, @PurchaserID int, @SupplierID int, @ProductID int;
DECLARE @POID bigint, @POLineID bigint, @InboundID bigint, @SourceID bigint;
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, DATEADD(hour, 7, @Now));
DECLARE @Tag varchar(16) = 'KKMAIN-QA02';
DECLARE @POCode varchar(40) = 'PO-' + @Tag;
DECLARE @ReceiptCode varchar(40) = 'IN-' + @Tag;
DECLARE @TierA int, @TierB int, @DamagedSlot int;
DECLARE @Serials TABLE (SerialNumber varchar(40) PRIMARY KEY, Placement char(1) NULL, Scenario nvarchar(200));
INSERT @Serials VALUES
    (@Tag + '-A01', 'A', N'Ghi nhận Tốt ở tầng A, quét lại để kiểm tra trùng.'),
    (@Tag + '-A02', 'A', N'Bỏ qua để test Thiếu; đếm lại ở tầng A nếu tìm thấy.'),
    (@Tag + '-A03', 'A', N'Ghi nhận Tốt ở tầng B để test sai vị trí.'),
    (@Tag + '-B01', 'B', N'Ghi nhận Tốt ở tầng B để test Khớp.'),
    (@Tag + '-B02', 'B', N'Ghi nhận Hỏng tại tầng B rồi chuyển đến ô dành cho hàng hỏng.'),
    (@Tag + '-B03', 'B', N'Ghi nhận Hỏng ở tầng A để test khác tình trạng và vị trí.'),
    (@Tag + '-W01', NULL, N'Ghi nhận Tốt ở hàng chờ xếp để test Khớp.'),
    (@Tag + '-W02', NULL, N'Ghi nhận Tốt ở tầng A để test đã tìm thấy trên kệ.'),
    (@Tag + '-W03', NULL, N'Bỏ qua để test Thiếu trong phần hàng chờ xếp.');
DECLARE @Quantity int = (SELECT COUNT(*) FROM @Serials);

BEGIN TRY
    IF @Tag IS NULL OR LEN(@Tag) = 0 OR @Tag COLLATE Latin1_General_100_BIN2 LIKE '%[^A-Za-z0-9-]%'
        THROW 51700, N'Tag chỉ gồm chữ không dấu, số và dấu gạch nối.', 1;
    BEGIN TRANSACTION;

    SELECT @WarehouseID = WarehouseID
    FROM dbo.Warehouse WITH (UPDLOCK, HOLDLOCK)
    WHERE WarehouseName = N'Kho chính EWMS' AND WarehouseType = 'MAIN' AND Status = 'ACTIVE';
    IF @@ROWCOUNT <> 1
        THROW 51701, N'Cần đúng một kho MAIN đang hoạt động có tên Kho chính EWMS.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Stocktake
        WHERE WarehouseID = @WarehouseID
          AND Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
          AND (ScopeType = 'WHOLE_WAREHOUSE' OR IncludeAwaitingPutaway = 1)
    )
        THROW 51702, N'Đang có đợt khóa toàn kho hoặc hàng chờ xếp. Hãy dùng database test riêng hoặc xử lý đợt đó trước.', 1;

    IF EXISTS (SELECT 1 FROM dbo.SerialItem WHERE SerialNumber LIKE @Tag + '-%')
       OR EXISTS (SELECT 1 FROM dbo.InboundItem WHERE ScannedSerial IN (@Tag + '-EXTRA01', @Tag + '-EXTRA02') AND IsActive = 1)
       OR EXISTS (SELECT 1 FROM dbo.PurchaseOrder WHERE POCode = @POCode)
       OR EXISTS (SELECT 1 FROM dbo.Inbound WHERE ReceiptCode = @ReceiptCode)
        THROW 51703, N'Tag đã tồn tại hoặc trùng mã. Đổi @Tag nếu muốn tạo bộ mới.', 1;

    SELECT TOP (1) @StaffID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'WAREHOUSE_STAFF' AND u.WarehouseID = @WarehouseID
    ORDER BY u.UserID;
    SELECT TOP (1) @PurchaserID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'PURCHASING_STAFF'
    ORDER BY u.UserID;
    IF @StaffID IS NULL OR @PurchaserID IS NULL
        THROW 51704, N'Cần nhân viên kho chính và nhân viên mua hàng đang hoạt động.', 1;

    DECLARE @Locked TABLE (LocationID int PRIMARY KEY);
    ;WITH LockedTree AS (
        SELECT sc.LocationID FROM dbo.StocktakeScope sc
        JOIN dbo.Stocktake st ON st.StocktakeID = sc.StocktakeID
        WHERE st.WarehouseID = @WarehouseID
          AND st.Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
        UNION ALL
        SELECT l.LocationID FROM dbo.StorageLocation l
        JOIN LockedTree parent ON l.ParentLocationID = parent.LocationID
        WHERE l.WarehouseID = @WarehouseID
    )
    INSERT @Locked SELECT DISTINCT LocationID FROM LockedTree OPTION (MAXRECURSION 100);

    -- Chọn vị trí trống từ cấu hình hiện có. Không thêm kệ hoặc đổi chính sách kho.
    DECLARE @Slots TABLE (
        LocationID int PRIMARY KEY, RowID int, RackID int NULL, ZoneID int, ZoneType varchar(16),
        ItemCondition varchar(8), IsFloor bit, LengthCm decimal(10,2), WidthCm decimal(10,2),
        HeightCm decimal(10,2) NULL, MaxWeightKg decimal(10,2) NULL
    );
    INSERT @Slots
    SELECT t.LocationID, r.LocationID, k.LocationID, z.LocationID, z.ZoneType, k.ConditionPolicy, 0,
        t.LengthCm, t.WidthCm, t.HeightCm, t.MaxWeightKg
    FROM dbo.StorageLocation t
    JOIN dbo.StorageLocation k ON k.LocationID = t.ParentLocationID AND k.LocationType = 'RACK'
    JOIN dbo.StorageLocation r ON r.LocationID = k.ParentLocationID AND r.LocationType = 'ROW' AND r.StorageType = 'RACK'
    JOIN dbo.StorageLocation z ON z.LocationID = r.ParentLocationID AND z.LocationType = 'ZONE'
    WHERE t.LocationType = 'TIER' AND t.CapacityMode = 'VOLUME' AND t.HeightCm > 0
      AND z.ParentLocationID IS NULL AND z.ZoneType IN ('STANDARD', 'RESERVE')
      AND k.ConditionPolicy IN ('GOOD', 'DAMAGED')
      AND t.WarehouseID = @WarehouseID AND k.WarehouseID = @WarehouseID
      AND r.WarehouseID = @WarehouseID AND z.WarehouseID = @WarehouseID
      AND t.Status = 'ACTIVE' AND k.Status = 'ACTIVE' AND r.Status = 'ACTIVE' AND z.Status = 'ACTIVE'
      AND t.LengthCm > 0 AND t.WidthCm > 0 AND (t.MaxWeightKg IS NULL OR t.MaxWeightKg > 0)
      AND NOT EXISTS (SELECT 1 FROM @Locked l WHERE l.LocationID IN (t.LocationID, k.LocationID, r.LocationID, z.LocationID))
      AND NOT EXISTS (SELECT 1 FROM dbo.SerialItem s WHERE s.CurrentLocationID = t.LocationID AND s.StockState = 'IN_WAREHOUSE');

    INSERT @Slots
    SELECT t.LocationID, r.LocationID, NULL, z.LocationID, z.ZoneType, t.ConditionPolicy, 1,
        t.LengthCm, t.WidthCm, t.HeightCm, t.MaxWeightKg
    FROM dbo.StorageLocation t
    JOIN dbo.StorageLocation r ON r.LocationID = t.ParentLocationID AND r.LocationType = 'ROW' AND r.StorageType = 'FLOOR'
    JOIN dbo.StorageLocation z ON z.LocationID = r.ParentLocationID AND z.LocationType = 'ZONE'
    WHERE t.LocationType = 'FLOOR_SLOT' AND t.CapacityMode = 'FLOOR_AREA' AND t.ConditionPolicy = 'DAMAGED'
      AND z.ParentLocationID IS NULL AND z.ZoneType IN ('STANDARD', 'RESERVE')
      AND t.WarehouseID = @WarehouseID AND r.WarehouseID = @WarehouseID AND z.WarehouseID = @WarehouseID
      AND t.Status = 'ACTIVE' AND r.Status = 'ACTIVE' AND z.Status = 'ACTIVE'
      AND t.LengthCm > 0 AND t.WidthCm > 0 AND (t.HeightCm IS NULL OR t.HeightCm > 0)
      AND (t.MaxWeightKg IS NULL OR t.MaxWeightKg > 0)
      AND NOT EXISTS (SELECT 1 FROM @Locked l WHERE l.LocationID IN (t.LocationID, r.LocationID, z.LocationID))
      AND NOT EXISTS (SELECT 1 FROM dbo.SerialItem s WHERE s.CurrentLocationID = t.LocationID AND s.StockState = 'IN_WAREHOUSE');

    DECLARE @Fit TABLE (ProductID int, LocationID int, PRIMARY KEY (ProductID, LocationID));
    INSERT @Fit
    SELECT p.ProductID, s.LocationID
    FROM dbo.Product p CROSS JOIN @Slots s
    WHERE p.Status = 'ACTIVE' AND p.LengthCm > 0 AND p.WidthCm > 0 AND p.HeightCm > 0 AND p.GrossWeightKg > 0
      AND p.LengthCm <= s.LengthCm AND p.WidthCm <= s.WidthCm AND (s.HeightCm IS NULL OR p.HeightCm <= s.HeightCm)
      AND (s.MaxWeightKg IS NULL OR s.MaxWeightKg >=
          CASE s.ItemCondition WHEN 'GOOD' THEN 5 ELSE 2 END * p.GrossWeightKg)
      AND ((s.IsFloor = 1 AND (s.LengthCm >= 2 * p.LengthCm OR s.WidthCm >= 2 * p.WidthCm))
          OR (s.IsFloor = 0 AND s.LengthCm * s.WidthCm * s.HeightCm >=
              CASE s.ItemCondition WHEN 'GOOD' THEN 5 ELSE 2 END * p.LengthCm * p.WidthCm * p.HeightCm))
      AND (s.ZoneType = 'RESERVE' OR EXISTS (
          SELECT 1 FROM dbo.ZoneStorageRule zr WHERE zr.ZoneLocationID = s.ZoneID
              AND zr.CategoryID = p.CategoryID AND (zr.BrandID IS NULL OR zr.BrandID = p.BrandID)
      ));

    SELECT TOP (1) @SupplierID = sp.SupplierID, @ProductID = sp.ProductID,
        @TierA = a.LocationID, @TierB = b.LocationID, @DamagedSlot = d.LocationID
    FROM dbo.SupplierProduct sp
    JOIN dbo.Supplier supplier ON supplier.SupplierID = sp.SupplierID AND supplier.Status = 'ACTIVE'
    JOIN @Fit fa ON fa.ProductID = sp.ProductID JOIN @Slots a ON a.LocationID = fa.LocationID AND a.ItemCondition = 'GOOD'
    JOIN @Fit fb ON fb.ProductID = sp.ProductID JOIN @Slots b ON b.LocationID = fb.LocationID AND b.ItemCondition = 'GOOD'
    JOIN @Fit fd ON fd.ProductID = sp.ProductID JOIN @Slots d ON d.LocationID = fd.LocationID AND d.ItemCondition = 'DAMAGED'
    WHERE a.LocationID < b.LocationID
    ORDER BY CASE WHEN a.RackID <> b.RackID AND a.RowID = b.RowID THEN 0
                  WHEN a.RackID <> b.RackID THEN 1 ELSE 2 END,
        CASE WHEN a.ZoneType = 'STANDARD' AND b.ZoneType = 'STANDARD' THEN 0 ELSE 1 END,
        sp.ProductID, sp.SupplierID, a.LocationID, b.LocationID, d.LocationID;

    IF @ProductID IS NULL
        THROW 51705, N'Cần một sản phẩm có nhà cung cấp đang hoạt động, hai tầng GOOD trống và một ô DAMAGED tương thích, đủ chỗ và không bị khóa.', 1;

    INSERT dbo.PurchaseOrder (SupplierID, WarehouseID, CreatedByUserID, POCode, OrderDate, ExpectedArrivalDate, Status, Note, CreatedAt)
    VALUES (@SupplierID, @WarehouseID, @PurchaserID, @POCode, @Today, @Today, 'DRAFT', N'Bộ dữ liệu kiểm kê ' + @Tag, @Now);
    SET @POID = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.PurchaseOrderLine (PurchaseOrderID, ProductID, OrderedQuantity, ClosedQuantity)
    VALUES (@POID, @ProductID, @Quantity, 0);
    SET @POLineID = CONVERT(bigint, SCOPE_IDENTITY());
    UPDATE dbo.PurchaseOrder SET Status = 'CONFIRMED', ConfirmedAt = @Now WHERE PurchaseOrderID = @POID;

    INSERT dbo.Inbound (PurchaseOrderID, CreatedByUserID, ReceiptCode, ReceiptDate, Status, CreatedAt)
    VALUES (@POID, @StaffID, @ReceiptCode, @Today, 'DRAFT', @Now);
    SET @InboundID = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.SupplySource (ProductID, WarehouseID, ItemCondition, SourceType, SupplyState, InboundID, EligibleQuantity, RemainingQuantity, AvailableFrom, CreatedAt)
    VALUES (@ProductID, @WarehouseID, 'GOOD', 'INBOUND', 'PHYSICAL', @InboundID, @Quantity, @Quantity, @Today, @Now);
    SET @SourceID = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.SerialItem (ProductID, SerialNumber, OriginType, OriginalInboundID, CurrentWarehouseID, CurrentLocationID,
        CurrentSupplySourceID, CurrentSupplyState, StockState, ItemCondition, IsQuarantined, RegisteredAt)
    SELECT @ProductID, SerialNumber, 'PURCHASE', @InboundID, @WarehouseID, NULL,
        @SourceID, 'PHYSICAL', 'IN_WAREHOUSE', 'GOOD', 0, @Now
    FROM @Serials;

    INSERT dbo.InboundItem (InboundID, POLineID, PurchaseOrderID, ProductID, ScannedSerial, SerialItemID, ReceivedCondition, ScannedAt, IsActive)
    SELECT @InboundID, @POLineID, @POID, @ProductID, s.SerialNumber, s.SerialItemID, 'GOOD', @Now, 1
    FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber;

    INSERT dbo.SerialMovement (SerialItemID, PerformedByUserID, EventType, OccurredAt, ToState, ToCondition,
        ToWarehouseID, ToLocationID, ToQuarantined, InboundID, Note)
    SELECT s.SerialItemID, @StaffID, 'PURCHASE_RECEIPT', @Now, 'IN_WAREHOUSE', 'GOOD',
        @WarehouseID, NULL, 0, @InboundID, N'Dữ liệu test kiểm kê kho chính'
    FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber;

    UPDATE dbo.Inbound SET Status = 'DONE', PerformedByUserID = @StaffID, CompletedAt = @Now WHERE InboundID = @InboundID;
    UPDATE dbo.PurchaseOrder SET Status = 'RECEIVED' WHERE PurchaseOrderID = @POID;

    UPDATE s SET CurrentLocationID = CASE t.Placement WHEN 'A' THEN @TierA ELSE @TierB END
    FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber
    WHERE t.Placement IS NOT NULL;

    INSERT dbo.SerialMovement (SerialItemID, PerformedByUserID, EventType, OccurredAt, FromState, ToState,
        FromCondition, ToCondition, FromWarehouseID, ToWarehouseID, FromLocationID, ToLocationID,
        FromQuarantined, ToQuarantined, Note)
    SELECT s.SerialItemID, @StaffID, 'PUTAWAY', @Now, 'IN_WAREHOUSE', 'IN_WAREHOUSE',
        'GOOD', 'GOOD', @WarehouseID, @WarehouseID, NULL, s.CurrentLocationID,
        0, 0, N'Xếp hàng test ' + @Tag
    FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber
    WHERE t.Placement IS NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT v.Label, z.LocationName AS Khu, r.LocationName AS Day, k.LocationName AS Ke,
    l.LocationName AS TangHoacOSan, l.LocationID
FROM (VALUES (N'Tầng A', @TierA), (N'Tầng B', @TierB), (N'Ô hàng hỏng gợi ý', @DamagedSlot)) v(Label, LocationID)
JOIN dbo.StorageLocation l ON l.LocationID = v.LocationID
LEFT JOIN dbo.StorageLocation k ON k.LocationID = l.ParentLocationID AND l.LocationType = 'TIER'
JOIN dbo.StorageLocation r ON r.LocationID = COALESCE(k.ParentLocationID, l.ParentLocationID)
JOIN dbo.StorageLocation z ON z.LocationID = r.ParentLocationID;

SELECT s.SerialNumber, p.ProductName, s.ItemCondition, s.CurrentLocationID,
    CASE t.Placement WHEN 'A' THEN N'Tầng A' WHEN 'B' THEN N'Tầng B' ELSE N'Chờ xếp' END AS ViTri, t.Scenario
FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber
JOIN dbo.Product p ON p.ProductID = s.ProductID
ORDER BY s.SerialNumber;

SELECT @ProductID AS ProductID, ProductName, @Tag + '-EXTRA01' AS SerialLaDeXacNhanThua,
    @Tag + '-EXTRA02' AS SerialLaDeXacNhanKhongThua
FROM dbo.Product WHERE ProductID = @ProductID;

SELECT s.SerialNumber, p.ProductName, s.ItemCondition
FROM dbo.SerialItem s JOIN dbo.Product p ON p.ProductID = s.ProductID
WHERE s.CurrentWarehouseID = @WarehouseID AND s.StockState = 'IN_WAREHOUSE'
  AND s.CurrentLocationID IS NULL AND NOT EXISTS (SELECT 1 FROM @Serials t WHERE t.SerialNumber = s.SerialNumber)
ORDER BY s.SerialNumber;
