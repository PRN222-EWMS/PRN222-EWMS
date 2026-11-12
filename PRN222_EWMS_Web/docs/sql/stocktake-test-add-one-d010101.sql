USE [WMSDB];
SET NOCOUNT ON;
IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Mở cửa sổ truy vấn mới rồi chạy toàn bộ file.', 16, 1);
    RETURN;
END;
SET XACT_ABORT ON;

DECLARE @WarehouseID int, @StaffID int, @PurchaserID int, @SupplierID int, @ProductID int;
DECLARE @POID bigint, @POLineID bigint, @InboundID bigint, @SourceID bigint;
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, DATEADD(hour, 7, @Now));
DECLARE @POCode varchar(40) = 'PO-KKMAIN-D010101';
DECLARE @ReceiptCode varchar(40) = 'IN-KKMAIN-D010101';
DECLARE @Serials TABLE (SerialNumber varchar(40) PRIMARY KEY);
INSERT @Serials VALUES ('KKMAIN-D010101-001');
DECLARE @TargetID int, @RackID int, @RowID int, @ZoneID int;
DECLARE @SourceSerial nvarchar(100);
DECLARE @Quantity int = (SELECT COUNT(*) FROM @Serials);

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT @WarehouseID = WarehouseID
    FROM dbo.Warehouse WITH (UPDLOCK, HOLDLOCK)
    WHERE WarehouseName = N'Kho chính EWMS' AND WarehouseType = 'MAIN' AND Status = 'ACTIVE';
    IF @@ROWCOUNT <> 1
        THROW 51801, N'Cần đúng một kho MAIN đang hoạt động có tên Kho chính EWMS.', 1;

    SELECT @TargetID = t.LocationID, @RackID = k.LocationID, @RowID = d.LocationID, @ZoneID = z.LocationID
    FROM dbo.StorageLocation t
    JOIN dbo.StorageLocation k ON k.LocationID = t.ParentLocationID
    JOIN dbo.StorageLocation d ON d.LocationID = k.ParentLocationID
    JOIN dbo.StorageLocation z ON z.LocationID = d.ParentLocationID
    WHERE t.WarehouseID = @WarehouseID AND k.WarehouseID = @WarehouseID
      AND d.WarehouseID = @WarehouseID AND z.WarehouseID = @WarehouseID
      AND t.Status = 'ACTIVE' AND k.Status = 'ACTIVE' AND d.Status = 'ACTIVE' AND z.Status = 'ACTIVE'
      AND z.LocationType = 'ZONE' AND z.ParentLocationID IS NULL AND z.LocationName = N'D'
      AND d.LocationType = 'ROW' AND d.StorageType = 'RACK' AND d.LocationName = N'Dãy 01'
      AND k.LocationType = 'RACK' AND k.LocationName IN (N'Kệ 01', N'K? 01')
      AND t.LocationType = 'TIER' AND t.LocationName IN (N'Tầng 01', N'T?ng 01');
    IF @@ROWCOUNT <> 1
        THROW 51802, N'Không xác định được đúng một tầng tại D / Dãy 01 / Kệ 01 / Tầng 01.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Stocktake st
        WHERE st.WarehouseID = @WarehouseID
          AND st.Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
          AND (st.ScopeType = 'WHOLE_WAREHOUSE' OR EXISTS (
              SELECT 1 FROM dbo.StocktakeScope sc WHERE sc.StocktakeID = st.StocktakeID
                AND sc.LocationID IN (@TargetID, @RackID, @RowID, @ZoneID)
          ))
    )
        THROW 51805, N'Tầng đích đang bị khóa kiểm kê. Xử lý đợt đang khóa trước khi thêm dữ liệu cho đợt mới.', 1;

    SELECT TOP (1) @ProductID = s.ProductID, @SourceSerial = s.SerialNumber
    FROM dbo.SerialItem s
    WHERE s.CurrentWarehouseID = @WarehouseID AND s.CurrentLocationID = @TargetID
      AND s.StockState = 'IN_WAREHOUSE' AND s.ItemCondition = 'GOOD'
    ORDER BY s.SerialItemID;
    IF @ProductID IS NULL
        THROW 51806, N'Không tìm thấy serial Tốt tại tầng đích để lấy cùng sản phẩm.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Product p
        JOIN dbo.StorageLocation t ON t.LocationID = @TargetID
        JOIN dbo.StorageLocation k ON k.LocationID = @RackID
        JOIN dbo.StorageLocation z ON z.LocationID = @ZoneID
        CROSS APPLY (
            SELECT SUM(op.GrossWeightKg) AS UsedWeight,
                SUM(op.LengthCm * op.WidthCm * op.HeightCm) AS UsedVolume,
                MAX(CASE WHEN o.ItemCondition <> 'GOOD' OR o.CurrentWarehouseID <> @WarehouseID
                    OR op.LengthCm <= 0 OR op.WidthCm <= 0 OR op.HeightCm <= 0 OR op.GrossWeightKg <= 0
                    OR op.LengthCm > occupied.LengthCm OR op.WidthCm > occupied.WidthCm
                    OR op.HeightCm > occupied.HeightCm THEN 1 ELSE 0 END) AS InvalidOccupant
            FROM dbo.SerialItem o JOIN dbo.Product op ON op.ProductID = o.ProductID
            JOIN dbo.StorageLocation occupied ON occupied.LocationID = o.CurrentLocationID
            WHERE o.CurrentLocationID = t.LocationID AND o.StockState = 'IN_WAREHOUSE'
        ) capacity
        WHERE p.ProductID = @ProductID AND p.Status = 'ACTIVE' AND k.ConditionPolicy = 'GOOD'
          AND p.LengthCm > 0 AND p.WidthCm > 0 AND p.HeightCm > 0 AND p.GrossWeightKg > 0
          AND t.CapacityMode = 'VOLUME' AND p.LengthCm <= t.LengthCm AND p.WidthCm <= t.WidthCm AND p.HeightCm <= t.HeightCm
          AND (t.MaxWeightKg IS NULL OR COALESCE(capacity.UsedWeight, 0) + p.GrossWeightKg <= t.MaxWeightKg)
          AND COALESCE(capacity.InvalidOccupant, 0) = 0
          AND COALESCE(capacity.UsedVolume, 0) + p.LengthCm * p.WidthCm * p.HeightCm <= t.LengthCm * t.WidthCm * t.HeightCm
          AND (z.ZoneType = 'RESERVE' OR z.ZoneType = 'STANDARD' AND EXISTS (
              SELECT 1 FROM dbo.ZoneStorageRule zr WHERE zr.ZoneLocationID = z.LocationID
                AND zr.CategoryID = p.CategoryID AND (zr.BrandID IS NULL OR zr.BrandID = p.BrandID)
          ))
    )
        THROW 51807, N'Tầng đích không phù hợp với sản phẩm hoặc không còn đủ sức chứa.', 1;

    IF EXISTS (SELECT 1 FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber)
       OR EXISTS (SELECT 1 FROM dbo.PurchaseOrder WHERE POCode = @POCode)
       OR EXISTS (SELECT 1 FROM dbo.Inbound WHERE ReceiptCode = @ReceiptCode)
        THROW 51803, N'Bộ dữ liệu KKMAIN-D010101 đã tồn tại hoặc trùng mã.', 1;

    SELECT TOP (1) @StaffID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'WAREHOUSE_STAFF' AND u.WarehouseID = @WarehouseID
    ORDER BY u.UserID;
    SELECT TOP (1) @PurchaserID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'PURCHASING_STAFF'
    ORDER BY u.UserID;
    SELECT TOP (1) @SupplierID = sp.SupplierID
    FROM dbo.SupplierProduct sp
    JOIN dbo.Supplier s ON s.SupplierID = sp.SupplierID
    JOIN dbo.Product p ON p.ProductID = sp.ProductID
    WHERE s.Status = 'ACTIVE' AND p.ProductID = @ProductID
    ORDER BY sp.ProductID, sp.SupplierID;

    IF @StaffID IS NULL OR @PurchaserID IS NULL OR @SupplierID IS NULL
        THROW 51804, N'Thiếu nhân viên kho chính, nhân viên mua hàng hoặc cặp nhà cung cấp/sản phẩm đang hoạt động.', 1;

    INSERT dbo.PurchaseOrder (SupplierID, WarehouseID, CreatedByUserID, POCode, OrderDate, ExpectedArrivalDate, Status, Note, CreatedAt)
    VALUES (@SupplierID, @WarehouseID, @PurchaserID, @POCode, @Today, @Today, 'DRAFT', N'Dữ liệu test kiểm kê kho chính', @Now);
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

    UPDATE s SET CurrentLocationID = @TargetID
    FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber;

    INSERT dbo.SerialMovement (SerialItemID, PerformedByUserID, EventType, OccurredAt,
        FromState, ToState, FromCondition, ToCondition, FromWarehouseID, ToWarehouseID,
        FromLocationID, ToLocationID, FromQuarantined, ToQuarantined, Note)
    SELECT s.SerialItemID, @StaffID, 'PUTAWAY', @Now, 'IN_WAREHOUSE', 'IN_WAREHOUSE',
        'GOOD', 'GOOD', @WarehouseID, @WarehouseID, NULL, @TargetID, 0, 0, N'Xếp serial test cùng tầng'
    FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT w.WarehouseName, s.SerialNumber, p.ProductName, s.StockState, s.ItemCondition, N'D → Dãy 01 → Kệ 01 → Tầng 01' AS ViTri,
    s.CurrentLocationID, @SourceSerial AS SerialCuCungSanPham
FROM dbo.SerialItem s
JOIN @Serials t ON t.SerialNumber = s.SerialNumber
JOIN dbo.Warehouse w ON w.WarehouseID = s.CurrentWarehouseID
JOIN dbo.Product p ON p.ProductID = s.ProductID
ORDER BY s.SerialNumber;
