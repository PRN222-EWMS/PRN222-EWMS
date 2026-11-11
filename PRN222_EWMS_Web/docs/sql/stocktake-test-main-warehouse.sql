USE [WMSDB];
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @WarehouseID int, @StaffID int, @PurchaserID int, @SupplierID int, @ProductID int;
DECLARE @POID bigint, @POLineID bigint, @InboundID bigint, @SourceID bigint;
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, DATEADD(hour, 7, @Now));
DECLARE @POCode varchar(40) = 'PO-KKMAIN-TEST01';
DECLARE @ReceiptCode varchar(40) = 'IN-KKMAIN-TEST01';
DECLARE @Serials TABLE (SerialNumber varchar(40) PRIMARY KEY);
INSERT @Serials VALUES ('KKMAIN-TEST01-001'), ('KKMAIN-TEST01-002'), ('KKMAIN-TEST01-003');
DECLARE @Quantity int = (SELECT COUNT(*) FROM @Serials);

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT @WarehouseID = WarehouseID
    FROM dbo.Warehouse WITH (UPDLOCK, HOLDLOCK)
    WHERE WarehouseName = N'Kho chính EWMS' AND WarehouseType = 'MAIN' AND Status = 'ACTIVE';
    IF @@ROWCOUNT <> 1
        THROW 51501, N'Cần đúng một kho MAIN đang hoạt động có tên Kho chính EWMS.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Stocktake
        WHERE WarehouseID = @WarehouseID
          AND Status IN ('COUNTING', 'RECOUNTING', 'REVIEWING', 'PENDING_APPROVAL')
          AND (ScopeType = 'WHOLE_WAREHOUSE' OR IncludeAwaitingPutaway = 1)
    )
        THROW 51502, N'Đang có đợt khóa toàn kho hoặc hàng chờ xếp. Hãy dùng database test riêng hoặc xử lý đợt đó trước.', 1;

    IF EXISTS (SELECT 1 FROM dbo.SerialItem s JOIN @Serials t ON t.SerialNumber = s.SerialNumber)
       OR EXISTS (SELECT 1 FROM dbo.PurchaseOrder WHERE POCode = @POCode)
       OR EXISTS (SELECT 1 FROM dbo.Inbound WHERE ReceiptCode = @ReceiptCode)
        THROW 51503, N'Bộ dữ liệu KKMAIN-TEST01 đã tồn tại hoặc trùng mã.', 1;

    SELECT TOP (1) @StaffID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'WAREHOUSE_STAFF' AND u.WarehouseID = @WarehouseID
    ORDER BY u.UserID;
    SELECT TOP (1) @PurchaserID = u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'PURCHASING_STAFF'
    ORDER BY u.UserID;
    SELECT TOP (1) @SupplierID = sp.SupplierID, @ProductID = sp.ProductID
    FROM dbo.SupplierProduct sp
    JOIN dbo.Supplier s ON s.SupplierID = sp.SupplierID
    JOIN dbo.Product p ON p.ProductID = sp.ProductID
    WHERE s.Status = 'ACTIVE' AND p.Status = 'ACTIVE'
    ORDER BY sp.ProductID, sp.SupplierID;

    IF @StaffID IS NULL OR @PurchaserID IS NULL OR @ProductID IS NULL
        THROW 51504, N'Thiếu nhân viên kho chính, nhân viên mua hàng hoặc cặp nhà cung cấp/sản phẩm đang hoạt động.', 1;

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

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT w.WarehouseName, s.SerialNumber, p.ProductName, s.StockState, s.ItemCondition, N'Chờ xếp' AS ViTri
FROM dbo.SerialItem s
JOIN @Serials t ON t.SerialNumber = s.SerialNumber
JOIN dbo.Warehouse w ON w.WarehouseID = s.CurrentWarehouseID
JOIN dbo.Product p ON p.ProductID = s.ProductID
ORDER BY s.SerialNumber;
