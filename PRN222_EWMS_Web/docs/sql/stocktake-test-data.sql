/*
EWMS - DU LIEU TEST KIEM KE / RECOUNT - 08/11/2026
Chi danh cho database phat trien. Mo SSMS va chay TOAN BO file.
Khong tu chay boi ung dung. Doi USE neu database khong ten WMSDB.

Them moi: 1 zone test + 2 day + 3 o san; 1 PO/2 dong; 1 Inbound DONE;
2 SupplySource PHYSICAL; 6 SerialItem/InboundItem/SerialMovement;
1 Stocktake PLANNED + 1 scope + 2 phan cong LOCATION.
Co the them toi da 2 lien ket SupplierProduct neu NCC chua cung ung SP.
Khong ALTER schema; khong sua/xoa ban ghi cu; khong doi User/mat khau.
Khong tao snapshot, observation, round, recount hay adjustment: test tren UI.
Chi UPDATE cac PO/Inbound vua tao de hoan tat bo du lieu ban dau.
AuditLog van duoc ghi boi trigger. Khong tat trigger hay reset ID.
File dung 1 lan: neu ma test da ton tai thi dung, khong chen trung.
Moi thay doi nam trong transaction; loi se rollback toan bo.

Can: schema 49 bang + ScopeKind + StocktakeRecountRequest;
1 Manager ACTIVE, 1 Purchasing Staff ACTIVE, 1 Supplier ACTIVE,
2 Product ACTIVE, 2 Warehouse Staff ACTIVE cung kho hoat dong.
Khong tao tai khoan: cac nhan vien can co Email de dang nhap ban ung dung moi.
Neu thieu thi script bao loi/khong chen; khong tu sua du lieu tai khoan.
Da co dinh tai khoan theo email:
  A: phamttrang1025@gmail.com (Warehouse Staff, dem day A).
  B: trangmooncat@gmail.com (Warehouse Staff, dem day B va recount day A).
  Manager: trangpham13061002@gmail.com.
Kho test lay tu 2 nhan vien tren; khong tu chon kho/nhan vien khac.

SAU KHI CHAY: xem cac bang ket qua cuoi file de biet Email, SP, vi tri, serial.
1. Nhan vien A: bat dau ST-TEST-01, ghi nhan theo bang huong dan, gui ket qua.
2. Nhan vien B: bat dau cung dot, quet KKTEST-006 tai B1/GOOD, gui ket qua.
3. Manager: Chi tiet -> Ra soat -> Recount SP thu nhat tai Day A, giao B.
4. B: bat dau yeu cau recount; quet lai 001..004 o vi tri GOC voi GOOD.
   Lan nay quet ca 002 de gia lap tim thay hang bi bo sot o vong dau.
5. Manager: SP1 da khop; SP2 van giu ket qua vong dau. Co the hoan tat.
   Muon test gui CEO: o vong recount tiep tuc bo qua 002 hoac de 004 DAMAGED,
   gui ket qua roi chon chenhlech -> Lap yeu cau dieu chinh -> gui CEO.
   GUI chua doi ton kho; duyet/ap dung la phan tich hop cua CEO.
6. Tuy chon serial thua: nhap KKTEST-UNKNOWN, chon SP1, GOOD/A1.
   Chi tao bang chung observation tren UI, khong tao SerialItem chinh thuc.

Chua thuc thi tren SQL Server cua ban. Khong can chay lai file schema.
*/
USE [WMSDB];
GO
IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Hay chay file khi khong co transaction dang mo.',16,1);
    RETURN;
END;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @PreviousActor sql_variant=SESSION_CONTEXT(N'ActorUserID');
DECLARE @PreviousReason sql_variant=SESSION_CONTEXT(N'AuditReason');
DECLARE @Now datetime2(3)=SYSUTCDATETIME();
DECLARE @Today date=CONVERT(date,DATEADD(hour,7,@Now));
DECLARE @StaffAEmail nvarchar(254)=N'phamttrang1025@gmail.com';
DECLARE @StaffBEmail nvarchar(254)=N'trangmooncat@gmail.com';
DECLARE @ManagerEmail nvarchar(254)=N'trangpham13061002@gmail.com';
DECLARE @WarehouseID int, @StaffA int, @StaffB int, @Manager int, @Purchaser int;
DECLARE @Supplier int, @Product1 int, @Product2 int;
DECLARE @Zone int, @RowA int, @RowB int, @A1 int, @A2 int, @B1 int;
DECLARE @PO bigint, @Line1 bigint, @Line2 bigint, @Inbound bigint, @Stocktake bigint;
DECLARE @Source1 bigint, @Source2 bigint, @InstallLock int;

BEGIN TRY
    BEGIN TRANSACTION;
    EXEC @InstallLock=sys.sp_getapplock @Resource=N'EWMS.Stocktake.TestData.v1',
        @LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
    IF @InstallLock<0 THROW 51300,N'Khong lay duoc khoa tao du lieu test.',1;

    IF COL_LENGTH(N'dbo.StocktakeAssignment',N'ScopeKind') IS NULL
       OR OBJECT_ID(N'dbo.StocktakeRecountRequest',N'U') IS NULL
        THROW 51301,N'Thieu ScopeKind hoac StocktakeRecountRequest. Chay cac file cap nhat cau truc truoc.',1;
    IF EXISTS(SELECT 1 FROM dbo.Stocktake WHERE StocktakeCode='ST-TEST-01')
       OR EXISTS(SELECT 1 FROM dbo.PurchaseOrder WHERE POCode='PO-KKTEST-01')
       OR EXISTS(SELECT 1 FROM dbo.SerialItem WHERE SerialNumber LIKE N'KKTEST-%')
       OR EXISTS(SELECT 1 FROM dbo.StorageLocation WHERE LocationName=N'ZONE TEST KIỂM KÊ')
        THROW 51302,N'Bo test da ton tai. Khong chen lai de tranh trung; dung du lieu da co.',1;

    -- Pin the three existing accounts supplied by Trang. Never fall back to another account.
    SELECT @StaffA=u.UserID,@WarehouseID=u.WarehouseID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
    WHERE u.Email=@StaffAEmail AND u.Status='ACTIVE' AND r.RoleName='WAREHOUSE_STAFF';
    SELECT @StaffB=u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
    WHERE u.Email=@StaffBEmail AND u.Status='ACTIVE' AND r.RoleName='WAREHOUSE_STAFF'
      AND u.WarehouseID=@WarehouseID;
    SELECT @Manager=u.UserID
    FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
    WHERE u.Email=@ManagerEmail AND u.Status='ACTIVE' AND r.RoleName='MANAGER';
    IF @StaffA IS NULL OR @StaffB IS NULL OR @StaffA=@StaffB OR @WarehouseID IS NULL
        THROW 51303,N'Khong tim thay dung 2 email Warehouse Staff ACTIVE cung kho. Kiem tra cac email khai bao o dau file; script khong tu chon nguoi khac.',1;
    IF @Manager IS NULL
        THROW 51306,N'Khong tim thay email Manager ACTIVE da khai bao. Script khong tu chon Manager khac.',1;

    -- Same first warehouse lock as Putaway and Start Stocktake.
    SELECT @WarehouseID=WarehouseID FROM dbo.Warehouse WITH(UPDLOCK,HOLDLOCK) WHERE WarehouseID=@WarehouseID;
    IF NOT EXISTS(SELECT 1 FROM dbo.Warehouse WHERE WarehouseID=@WarehouseID AND Status='ACTIVE'
                  AND (WarehouseType='MAIN' OR @Today BETWEEN RentalStartDate AND RentalEndDate))
        THROW 51307,N'Kho cua 2 nhan vien khong hoat dong hoac nam ngoai thoi han thue.',1;
    IF EXISTS(SELECT 1 FROM dbo.Stocktake WHERE WarehouseID=@WarehouseID AND ScopeType='WHOLE_WAREHOUSE'
              AND Status IN ('COUNTING','RECOUNTING','REVIEWING','PENDING_APPROVAL'))
        THROW 51308,N'Kho cua 2 nhan vien dang bi khoa kiem ke toan kho. Can giai quyet dot cu truoc; script khong tu sua dot cu.',1;
    SELECT TOP(1) @Purchaser=u.UserID FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
    WHERE u.Status='ACTIVE' AND r.RoleName='PURCHASING_STAFF' ORDER BY u.UserID;
    SELECT TOP(1) @Supplier=SupplierID FROM dbo.Supplier WHERE Status='ACTIVE' ORDER BY SupplierID;
    SELECT TOP(1) @Product1=ProductID FROM dbo.Product WHERE Status='ACTIVE' ORDER BY ProductID;
    SELECT TOP(1) @Product2=ProductID FROM dbo.Product WHERE Status='ACTIVE' AND ProductID<>@Product1 ORDER BY ProductID;
    IF @Manager IS NULL OR @Purchaser IS NULL OR @Supplier IS NULL OR @Product2 IS NULL
        THROW 51304,N'Thieu Manager/Purchasing Staff ACTIVE, Supplier ACTIVE hoac 2 Product ACTIVE. Khong chen du lieu.',1;
    IF EXISTS(SELECT 1 FROM dbo.[User] WHERE UserID IN (@StaffA,@StaffB,@Manager)
              AND NULLIF(LTRIM(RTRIM(Email)),N'') IS NULL)
    BEGIN
        SELECT UserID,FullName,Email,Status FROM dbo.[User] WHERE UserID IN (@StaffA,@StaffB,@Manager);
        THROW 51305,N'Nhan vien test hoac Manager chua co Email de dang nhap. Hay cau hinh tai khoan tren chuc nang cua nhom roi chay lai; script khong doi tai khoan.',1;
    END;

    EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=NULL;
    EXEC sys.sp_set_session_context @key=N'AuditReason',@value=N'Tao bo du lieu test kiem ke KKTEST, khong sua ban ghi cu';

    INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ZoneType,Status)
    VALUES(@WarehouseID,NULL,N'ZONE TEST KIỂM KÊ','ZONE','RESERVE','ACTIVE');
    SET @Zone=CONVERT(int,SCOPE_IDENTITY());
    INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,Status)
    VALUES(@WarehouseID,@Zone,N'Dãy A - kiểm kê','ROW','FLOOR','ACTIVE');
    SET @RowA=CONVERT(int,SCOPE_IDENTITY());
    INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,Status)
    VALUES(@WarehouseID,@Zone,N'Dãy B - kiểm kê','ROW','FLOOR','ACTIVE');
    SET @RowB=CONVERT(int,SCOPE_IDENTITY());
    INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
    VALUES(@WarehouseID,@RowA,N'Ô A1 - kiểm kê','FLOOR_SLOT','GOOD','FLOOR_AREA',1000,1000,10000,'ACTIVE');
    SET @A1=CONVERT(int,SCOPE_IDENTITY());
    INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
    VALUES(@WarehouseID,@RowA,N'Ô A2 - kiểm kê','FLOOR_SLOT','GOOD','FLOOR_AREA',1000,1000,10000,'ACTIVE');
    SET @A2=CONVERT(int,SCOPE_IDENTITY());
    INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
    VALUES(@WarehouseID,@RowB,N'Ô B1 - kiểm kê','FLOOR_SLOT','GOOD','FLOOR_AREA',1000,1000,10000,'ACTIVE');
    SET @B1=CONVERT(int,SCOPE_IDENTITY());

    INSERT dbo.SupplierProduct(SupplierID,ProductID)
    SELECT @Supplier,p.ProductID FROM dbo.Product p WHERE p.ProductID IN (@Product1,@Product2)
      AND NOT EXISTS(SELECT 1 FROM dbo.SupplierProduct sp WHERE sp.SupplierID=@Supplier AND sp.ProductID=p.ProductID);
    INSERT dbo.PurchaseOrder(SupplierID,WarehouseID,CreatedByUserID,POCode,OrderDate,ExpectedArrivalDate,Status,Note,CreatedAt)
    VALUES(@Supplier,@WarehouseID,@Purchaser,'PO-KKTEST-01',@Today,@Today,'DRAFT',N'Chứng từ riêng cho bộ test kiểm kê KKTEST',@Now);
    SET @PO=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.PurchaseOrderLine(PurchaseOrderID,ProductID,OrderedQuantity,ClosedQuantity) VALUES(@PO,@Product1,4,0);
    SET @Line1=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.PurchaseOrderLine(PurchaseOrderID,ProductID,OrderedQuantity,ClosedQuantity) VALUES(@PO,@Product2,2,0);
    SET @Line2=CONVERT(bigint,SCOPE_IDENTITY());
    UPDATE dbo.PurchaseOrder SET Status='CONFIRMED',ConfirmedAt=@Now WHERE PurchaseOrderID=@PO;
    INSERT dbo.Inbound(PurchaseOrderID,CreatedByUserID,ReceiptCode,ReceiptDate,Status,CreatedAt)
    VALUES(@PO,@StaffA,'IN-KKTEST-01',@Today,'DRAFT',@Now);
    SET @Inbound=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.SupplySource(ProductID,WarehouseID,ItemCondition,SourceType,SupplyState,InboundID,EligibleQuantity,RemainingQuantity,AvailableFrom,CreatedAt)
    VALUES(@Product1,@WarehouseID,'GOOD','INBOUND','PHYSICAL',@Inbound,4,4,@Today,@Now);
    SET @Source1=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.SupplySource(ProductID,WarehouseID,ItemCondition,SourceType,SupplyState,InboundID,EligibleQuantity,RemainingQuantity,AvailableFrom,CreatedAt)
    VALUES(@Product2,@WarehouseID,'GOOD','INBOUND','PHYSICAL',@Inbound,2,2,@Today,@Now);
    SET @Source2=CONVERT(bigint,SCOPE_IDENTITY());

    DECLARE @Seed TABLE(Serial nvarchar(100),ProductID int,POLineID bigint,SourceID bigint,LocationID int,Instruction nvarchar(500));
    INSERT @Seed VALUES
    (N'KKTEST-001',@Product1,@Line1,@Source1,@A1,N'A quét GOOD / A1: khớp'),
    (N'KKTEST-002',@Product1,@Line1,@Source1,@A1,N'A bỏ qua ở vòng đầu: thiếu khi đóng vòng'),
    (N'KKTEST-003',@Product1,@Line1,@Source1,@A1,N'A quét GOOD / A2: sai vị trí'),
    (N'KKTEST-004',@Product1,@Line1,@Source1,@A2,N'A quét DAMAGED / A2: khác tình trạng'),
    (N'KKTEST-005',@Product2,@Line2,@Source2,@A1,N'A quét GOOD / A1: sản phẩm khác, giữ kết quả khi recount SP1'),
    (N'KKTEST-006',@Product2,@Line2,@Source2,@B1,N'B quét GOOD / B1: khớp');

    INSERT dbo.SerialItem(ProductID,SerialNumber,OriginType,OriginalInboundID,CurrentWarehouseID,CurrentLocationID,
        CurrentSupplySourceID,CurrentSupplyState,StockState,ItemCondition,IsQuarantined,RegisteredAt)
    SELECT ProductID,Serial,'PURCHASE',@Inbound,@WarehouseID,LocationID,SourceID,'PHYSICAL','IN_WAREHOUSE','GOOD',0,@Now FROM @Seed;
    INSERT dbo.InboundItem(InboundID,POLineID,PurchaseOrderID,ProductID,ScannedSerial,SerialItemID,ReceivedCondition,ScannedAt,IsActive)
    SELECT @Inbound,t.POLineID,@PO,t.ProductID,t.Serial,s.SerialItemID,'GOOD',@Now,1
    FROM @Seed t JOIN dbo.SerialItem s ON s.SerialNumber=t.Serial;
    INSERT dbo.SerialMovement(SerialItemID,PerformedByUserID,EventType,OccurredAt,ToState,ToCondition,ToWarehouseID,ToLocationID,ToQuarantined,InboundID,Note)
    SELECT s.SerialItemID,@StaffA,'PURCHASE_RECEIPT',@Now,'IN_WAREHOUSE','GOOD',@WarehouseID,t.LocationID,0,@Inbound,N'Tồn đầu riêng phục vụ kiểm thử KKTEST'
    FROM @Seed t JOIN dbo.SerialItem s ON s.SerialNumber=t.Serial;
    UPDATE dbo.Inbound SET Status='DONE',PerformedByUserID=@StaffA,CompletedAt=@Now WHERE InboundID=@Inbound;
    UPDATE dbo.PurchaseOrder SET Status='RECEIVED' WHERE PurchaseOrderID=@PO;

    INSERT dbo.Stocktake(WarehouseID,CreatedByUserID,StocktakeCode,ScopeType,Status,Note,CreatedAt,IncludeAwaitingPutaway,PlannedAt)
    VALUES(@WarehouseID,@Manager,'ST-TEST-01','LOCATIONS','PLANNED',N'Test kiểm kê: A đếm dãy A, B đếm dãy B; recount SP1 dãy A giao B.',@Now,0,@Now);
    SET @Stocktake=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.StocktakeScope(StocktakeID,LocationID,WarehouseID) VALUES(@Stocktake,@Zone,@WarehouseID);
    INSERT dbo.StocktakeAssignment(StocktakeID,UserID,LocationID,Status,AssignedAt,WarehouseID,ScopeKind)
    VALUES(@Stocktake,@StaffA,@RowA,'ASSIGNED',@Now,@WarehouseID,'LOCATION'),
          (@Stocktake,@StaffB,@RowB,'ASSIGNED',@Now,@WarehouseID,'LOCATION');

    EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=@PreviousActor;
    EXEC sys.sp_set_session_context @key=N'AuditReason',@value=@PreviousReason;
    COMMIT TRANSACTION;

    SELECT @Stocktake AS StocktakeID,'ST-TEST-01' AS StocktakeCode,w.WarehouseName,
           @RowA AS RecountLocationID,@Product1 AS RecountProductID
    FROM dbo.Warehouse w WHERE w.WarehouseID=@WarehouseID;
    SELECT CASE u.UserID WHEN @StaffA THEN N'Nhân viên A - kiểm dãy A' WHEN @StaffB THEN N'Nhân viên B - kiểm dãy B và đếm lại dãy A' ELSE N'Manager' END AS TestRole,
           u.UserID,u.FullName,u.Email FROM dbo.[User] u WHERE u.UserID IN (@StaffA,@StaffB,@Manager);
    SELECT t.Serial,p.ProductCode,p.ProductName,l.LocationName AS OriginalLocation,t.Instruction AS FirstRoundInstruction
    FROM @Seed t JOIN dbo.Product p ON p.ProductID=t.ProductID JOIN dbo.StorageLocation l ON l.LocationID=t.LocationID ORDER BY t.Serial;
    PRINT N'Thanh cong. Hay bat dau ST-TEST-01 tren ung dung. Chua tao snapshot/ket qua kiem ke.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=@PreviousActor;
    EXEC sys.sp_set_session_context @key=N'AuditReason',@value=@PreviousReason;
    THROW;
END CATCH;
