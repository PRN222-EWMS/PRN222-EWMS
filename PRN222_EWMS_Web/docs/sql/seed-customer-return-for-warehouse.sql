-- Chạy trong SSMS, chọn đúng database WMSDB trước khi Execute.
-- Tạo 1 đơn trả hàng DRAFT và 1 dòng hàng từ Serial đã xuất thực tế.
-- Không tạo phiếu nhận trả, không thay đổi Serial hoặc tồn kho.
-- Nếu có nhiều nhân viên kho, điền UserID đang đăng nhập để chọn đúng kho.
DECLARE @WarehouseStaffUserID int = NULL;

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'WMSDB'
    THROW 51000, N'Hãy chọn database WMSDB trước khi chạy script.', 1;

DECLARE @SelectedStaffUserID int;
DECLARE @WarehouseID int;
DECLARE @CreatorUserID int;
DECLARE @SalesOrderID bigint;
DECLARE @OutboundID bigint;
DECLARE @OutboundItemID bigint;
DECLARE @SOLineID bigint;
DECLARE @ProductID int;
DECLARE @SerialNumber nvarchar(100);
DECLARE @ReturnCode varchar(40);
DECLARE @ReturnOrderID bigint;
DECLARE @Created bit = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    IF @WarehouseStaffUserID IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM dbo.[User] u
           JOIN dbo.[Role] r ON r.RoleID = u.RoleID
           WHERE u.UserID = @WarehouseStaffUserID
             AND u.Status = 'ACTIVE'
             AND r.RoleName = 'WAREHOUSE_STAFF'
             AND u.WarehouseID IS NOT NULL
       )
        THROW 51001, N'UserID chỉ định không phải nhân viên kho đang hoạt động.', 1;

    SELECT TOP (1)
        @SelectedStaffUserID = staff.UserID,
        @WarehouseID = staff.WarehouseID,
        @SalesOrderID = oi.SalesOrderID,
        @OutboundID = oi.OutboundID,
        @OutboundItemID = oi.OutboundItemID,
        @SOLineID = oi.SOLineID,
        @ProductID = oi.ProductID,
        @SerialNumber = si.SerialNumber
    FROM dbo.OutboundItem oi
    JOIN dbo.Outbound ob ON ob.OutboundID = oi.OutboundID
    JOIN dbo.SalesOrder so ON so.SalesOrderID = oi.SalesOrderID
    JOIN dbo.SerialItem si ON si.SerialItemID = oi.SerialItemID
    JOIN dbo.[User] staff ON staff.WarehouseID = so.WarehouseID
    JOIN dbo.[Role] staffRole ON staffRole.RoleID = staff.RoleID
    WHERE oi.IsActive = 1
      AND ob.Status = 'DONE'
      AND ob.SalesOrderID = so.SalesOrderID
      AND oi.ProductID = si.ProductID
      AND si.StockState = 'SOLD'
      AND staff.Status = 'ACTIVE'
      AND staffRole.RoleName = 'WAREHOUSE_STAFF'
      AND (@WarehouseStaffUserID IS NULL OR staff.UserID = @WarehouseStaffUserID)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.ReturnReceiptItem rri
          WHERE rri.OutboundItemID = oi.OutboundItemID AND rri.IsActive = 1
      )
      AND (
          EXISTS (
              SELECT 1 FROM dbo.CustomerReturnOrder cro
              WHERE cro.ReturnCode = CONCAT('RT-TEST-', oi.OutboundItemID)
          )
          OR NOT EXISTS (
              SELECT 1 FROM dbo.CustomerReturnOrderLine crol
              JOIN dbo.CustomerReturnOrder cro ON cro.ReturnOrderID = crol.ReturnOrderID
              WHERE crol.SOLineID = oi.SOLineID
                AND cro.Status <> 'CANCELLED'
          )
      )
    ORDER BY
        CASE WHEN EXISTS (
            SELECT 1 FROM dbo.CustomerReturnOrder cro
            WHERE cro.ReturnCode = CONCAT('RT-TEST-', oi.OutboundItemID)
        ) THEN 0 ELSE 1 END,
        oi.OutboundItemID DESC;

    IF @OutboundItemID IS NULL
        THROW 51002, N'Không có Serial đã xuất (Outbound DONE, Serial SOLD) chưa được nhận trả trong kho của nhân viên này.', 1;

    SET @ReturnCode = CONCAT('RT-TEST-', @OutboundItemID);

    SELECT @ReturnOrderID = cro.ReturnOrderID
    FROM dbo.CustomerReturnOrder cro
    WHERE cro.ReturnCode = @ReturnCode;

    IF @ReturnOrderID IS NULL
    BEGIN
        SELECT TOP (1) @CreatorUserID = u.UserID
        FROM dbo.[User] u
        JOIN dbo.[Role] r ON r.RoleID = u.RoleID
        WHERE u.Status = 'ACTIVE' AND r.RoleName = 'SALES_STAFF'
        ORDER BY CASE WHEN u.UserID = (
            SELECT so.CreatedByUserID FROM dbo.SalesOrder so
            WHERE so.SalesOrderID = @SalesOrderID
        ) THEN 0 ELSE 1 END, u.UserID;

        IF @CreatorUserID IS NULL
            THROW 51003, N'Không có tài khoản SALES_STAFF đang hoạt động để tạo đơn trả hàng.', 1;

        EXEC sys.sp_set_session_context @key = N'ActorUserID', @value = @CreatorUserID;

        INSERT INTO dbo.CustomerReturnOrder
            (SalesOrderID, WarehouseID, CreatedByUserID, ReturnCode, Status, Reason)
        VALUES
            (@SalesOrderID, @WarehouseID, @CreatorUserID, @ReturnCode,
             'DRAFT', N'Dữ liệu thử nghiệm: khách trả 1 sản phẩm đã mua.');

        SET @ReturnOrderID = CONVERT(bigint, SCOPE_IDENTITY());

        INSERT INTO dbo.CustomerReturnOrderLine
            (ReturnOrderID, SOLineID, RequestedQuantity, CancelledQuantity,
             SalesOrderID, ProductID)
        VALUES
            (@ReturnOrderID, @SOLineID, 1, 0, @SalesOrderID, @ProductID);

        SET @Created = 1;
    END;

    COMMIT TRANSACTION;
    EXEC sys.sp_set_session_context @key = N'ActorUserID', @value = NULL;

    SELECT
        CASE WHEN @Created = 1 THEN N'Đã tạo' ELSE N'Đã có từ lần chạy trước' END AS Result,
        @SelectedStaffUserID AS WarehouseStaffUserID,
        @WarehouseID AS WarehouseID,
        @ReturnOrderID AS ReturnOrderID,
        @ReturnCode AS ReturnCode,
        so.SOCode AS SalesOrderCode,
        ob.IssueCode AS OutboundCode,
        @SerialNumber AS SerialCanQuet
    FROM dbo.SalesOrder so
    JOIN dbo.Outbound ob ON ob.OutboundID = @OutboundID
    WHERE so.SalesOrderID = @SalesOrderID;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    EXEC sys.sp_set_session_context @key = N'ActorUserID', @value = NULL;
    THROW;
END CATCH;
