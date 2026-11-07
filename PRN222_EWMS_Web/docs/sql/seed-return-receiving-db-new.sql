-- Du lieu test cho man Nhan hang tra, dua tren Db_new.sql (WMSDB).
-- Moi lan chay tao mot don ban + phieu xuat DONE + don tra OPEN cho 1 Serial.
-- Script su dung 1 Serial GOOD dang ton kho, nen se giam ton kho test 1 don vi.
-- Thay UserID neu dang nhap bang nhan vien kho khac (dump co UserID 19, kho 1).
USE [WMSDB];
GO
DECLARE @WarehouseStaffUserID int = 19;

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @WarehouseID int;
DECLARE @SalesStaffUserID int;
DECLARE @CustomerID int;
DECLARE @CustomerName nvarchar(200);
DECLARE @CustomerAddress nvarchar(500);
DECLARE @CustomerPhone nvarchar(30);
DECLARE @SerialItemID bigint;
DECLARE @SerialNumber nvarchar(100);
DECLARE @ProductID int;
DECLARE @SupplySourceID bigint;
DECLARE @FromLocationID int;
DECLARE @SalesOrderID bigint;
DECLARE @SOLineID bigint;
DECLARE @OutboundID bigint;
DECLARE @OutboundItemID bigint;
DECLARE @ReturnOrderID bigint;
DECLARE @SeedKey varchar(12) = LEFT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 12);
DECLARE @Now datetime2(3) = SYSUTCDATETIME();
DECLARE @TodayVN date = CONVERT(date, DATEADD(hour, 7, @Now));
DECLARE @SOCode varchar(40) = CONCAT('SO-RT-TEST-', @SeedKey);
DECLARE @IssueCode varchar(40) = CONCAT('OUT-RT-TEST-', @SeedKey);
DECLARE @ReturnCode varchar(40) = CONCAT('RT-TEST-', @SeedKey);

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT @WarehouseID = u.WarehouseID
    FROM dbo.[User] u
    JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.UserID = @WarehouseStaffUserID
      AND u.Status = 'ACTIVE'
      AND r.RoleName = 'WAREHOUSE_STAFF';

    IF @WarehouseID IS NULL
        THROW 51001, N'UserID phai la nhan vien kho dang hoat dong va da duoc gan kho.', 1;

    SELECT TOP (1) @SalesStaffUserID = u.UserID
    FROM dbo.[User] u
    JOIN dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE u.Status = 'ACTIVE' AND r.RoleName = 'SALES_STAFF'
    ORDER BY u.UserID DESC;

    IF @SalesStaffUserID IS NULL
        THROW 51002, N'Khong co nhan vien ban hang dang hoat dong.', 1;

    SELECT TOP (1)
        @CustomerID = c.CustomerID,
        @CustomerName = c.CustomerName,
        @CustomerAddress = c.Address,
        @CustomerPhone = c.Phone
    FROM dbo.Customer c
    WHERE c.Status = 'ACTIVE'
    ORDER BY c.CustomerID;

    IF @CustomerID IS NULL
        THROW 51003, N'Khong co khach hang dang hoat dong.', 1;

    -- Chi dung Serial chua duoc xuat, co nguon ton kho thuc va du so luong.
    SELECT TOP (1)
        @SerialItemID = si.SerialItemID,
        @SerialNumber = si.SerialNumber,
        @ProductID = si.ProductID,
        @SupplySourceID = ss.SupplySourceID,
        @FromLocationID = si.CurrentLocationID
    FROM dbo.SerialItem si WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.SupplySource ss WITH (UPDLOCK, HOLDLOCK)
      ON ss.SupplySourceID = si.CurrentSupplySourceID
     AND ss.ProductID = si.ProductID
     AND ss.WarehouseID = si.CurrentWarehouseID
     AND ss.ItemCondition = si.ItemCondition
     AND ss.SupplyState = si.CurrentSupplyState
    JOIN dbo.Product p ON p.ProductID = si.ProductID
    WHERE si.CurrentWarehouseID = @WarehouseID
      AND si.StockState = 'IN_WAREHOUSE'
      AND si.ItemCondition = 'GOOD'
      AND si.IsQuarantined = 0
      AND ss.SupplyState = 'PHYSICAL'
      AND ss.ItemCondition = 'GOOD'
      AND ss.RemainingQuantity > 0
      AND ss.EligibleQuantity > 0
      AND p.Status = 'ACTIVE'
      AND NOT EXISTS (
          SELECT 1 FROM dbo.OutboundItem oi
          WHERE oi.SerialItemID = si.SerialItemID AND oi.IsActive = 1
      )
      AND NOT EXISTS (
          SELECT 1 FROM dbo.Reservation r
          WHERE r.SupplySourceID = ss.SupplySourceID
            AND r.Status = 'ACTIVE'
            AND r.ReservedQuantity > r.ConsumedQuantity + r.ReleasedQuantity
      )
    ORDER BY si.SerialItemID DESC;

    IF @SerialItemID IS NULL
        THROW 51004, N'Kho nay khong co Serial GOOD san sang xuat. Hay nhap kho 1 Serial truoc.', 1;

    EXEC sys.sp_set_session_context @key = N'ActorUserID', @value = @SalesStaffUserID;
    EXEC sys.sp_set_session_context @key = N'AuditReason', @value = N'Seed test nhan hang tra';

    INSERT INTO dbo.SalesOrder
        (CustomerID, WarehouseID, CreatedByUserID, SOCode, OrderDate,
         Status, HasShortage, Note, CreatedAt, RequestedDate,
         CustomerNameSnapshot, CustomerAddressSnapshot, CustomerPhoneSnapshot)
    VALUES
        (@CustomerID, @WarehouseID, @SalesStaffUserID, @SOCode, @TodayVN,
         'DRAFT', 0, N'Don ban mau de test nhan hang tra', @Now, @TodayVN,
         @CustomerName, @CustomerAddress, @CustomerPhone);

    SET @SalesOrderID = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT INTO dbo.SalesOrderLine
        (SalesOrderID, ProductID, RequestedQuantity, CancelledQuantity)
    VALUES (@SalesOrderID, @ProductID, 1, 0);

    SET @SOLineID = CONVERT(bigint, SCOPE_IDENTITY());

    UPDATE dbo.SalesOrder
    SET Status = 'SUBMITTED', SubmittedAt = @Now
    WHERE SalesOrderID = @SalesOrderID;

    INSERT INTO dbo.Outbound
        (SalesOrderID, CreatedByUserID, AssignedToUserID, IssueCode,
         IssueDate, Status, IsActivePicking, RecipientName, Note, CreatedAt,
         CustomerNameSnapshot, CustomerAddressSnapshot, CustomerPhoneSnapshot)
    VALUES
        (@SalesOrderID, @SalesStaffUserID, @WarehouseStaffUserID, @IssueCode,
         @TodayVN, 'DRAFT', 0, @CustomerName,
         N'Phieu xuat mau de test nhan hang tra', @Now,
         @CustomerName, @CustomerAddress, @CustomerPhone);

    SET @OutboundID = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT INTO dbo.OutboundItem
        (OutboundID, SOLineID, SerialItemID, SourceAtIssueID, ScannedAt,
         IsActive, SalesOrderID, ProductID, WarehouseID, ItemCondition, SupplyState)
    VALUES
        (@OutboundID, @SOLineID, @SerialItemID, @SupplySourceID, @Now,
         1, @SalesOrderID, @ProductID, @WarehouseID, 'GOOD', 'PHYSICAL');

    SET @OutboundItemID = CONVERT(bigint, SCOPE_IDENTITY());

    -- Ghi nhan nghiep vu xuat ban va chuyen Serial sang SOLD.
    INSERT INTO dbo.SerialMovement
        (SerialItemID, PerformedByUserID, EventType, OccurredAt,
         FromState, ToState, FromCondition, ToCondition,
         FromWarehouseID, ToWarehouseID, FromLocationID,
         FromQuarantined, ToQuarantined, OutboundID, Note)
    VALUES
        (@SerialItemID, @WarehouseStaffUserID, 'SALES_ISSUE', @Now,
         'IN_WAREHOUSE', 'SOLD', 'GOOD', 'GOOD',
         @WarehouseID, NULL, @FromLocationID,
         0, 0, @OutboundID, N'Seed test nhan hang tra');

    UPDATE dbo.SupplySource
    SET EligibleQuantity = EligibleQuantity - 1,
        RemainingQuantity = RemainingQuantity - 1
    WHERE SupplySourceID = @SupplySourceID
      AND EligibleQuantity > 0 AND RemainingQuantity > 0;

    IF @@ROWCOUNT <> 1
        THROW 51005, N'Nguon ton kho da het trong luc tao du lieu test.', 1;

    UPDATE dbo.SerialItem
    SET StockState = 'SOLD',
        CurrentWarehouseID = NULL,
        CurrentLocationID = NULL,
        CurrentSupplySourceID = NULL,
        CurrentSupplyState = NULL
    WHERE SerialItemID = @SerialItemID AND StockState = 'IN_WAREHOUSE';

    IF @@ROWCOUNT <> 1
        THROW 51006, N'Trang thai Serial da thay doi trong luc tao du lieu test.', 1;

    UPDATE dbo.Outbound
    SET Status = 'DONE', CompletedAt = @Now
    WHERE OutboundID = @OutboundID;

    UPDATE dbo.SalesOrder
    SET Status = 'FULFILLED'
    WHERE SalesOrderID = @SalesOrderID;

    INSERT INTO dbo.CustomerReturnOrder
        (SalesOrderID, WarehouseID, CreatedByUserID, ReturnCode,
         CreatedAt, Status, Reason)
    VALUES
        (@SalesOrderID, @WarehouseID, @SalesStaffUserID, @ReturnCode,
         @Now, 'OPEN', N'Khach tra 1 san pham; kiem tra Serial va tinh trang.');

    SET @ReturnOrderID = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT INTO dbo.CustomerReturnOrderLine
        (ReturnOrderID, SOLineID, RequestedQuantity, CancelledQuantity,
         SalesOrderID, ProductID)
    VALUES
        (@ReturnOrderID, @SOLineID, 1, 0, @SalesOrderID, @ProductID);

    COMMIT TRANSACTION;
    EXEC sys.sp_set_session_context @key = N'ActorUserID', @value = NULL;
    EXEC sys.sp_set_session_context @key = N'AuditReason', @value = NULL;

    SELECT
        @WarehouseStaffUserID AS WarehouseStaffUserID,
        @WarehouseID AS WarehouseID,
        @SOCode AS SalesOrderCode,
        @IssueCode AS OutboundCode,
        @ReturnCode AS ReturnOrderCode,
        @SerialNumber AS SerialCanQuet,
        @ReturnOrderID AS ReturnOrderID,
        @OutboundItemID AS OutboundItemID;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    EXEC sys.sp_set_session_context @key = N'ActorUserID', @value = NULL;
    EXEC sys.sp_set_session_context @key = N'AuditReason', @value = NULL;
    THROW;
END CATCH;
