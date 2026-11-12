/*
EWMS - BO DU LIEU TEST KIEM KE MO RONG - 10/11/2026
Mo SSMS, chon database phat trien va chay TOAN BO file.
Script chi them du lieu co tien to @Tag, khong ALTER schema/khong xoa/reset.
Khong sua User, mat khau, serial, vi tri, chung tu hay ket qua da co.
UPDATE chi ap dung PO/Inbound moi tao trong transaction nay.
Khong tao snapshot/round/observation/recount/adjustment; phai test tren UI.
Khong tu duyet, khong tu tru/tang ton do kiem ke.

Mac dinh Tag=QA261110. Muon tao bo test doc lap lan khac, doi Tag truoc khi chay.
Khong chay lai cung Tag: script se dung, khong chen trung.
Can schema 49 bang + ScopeKind + StocktakeRecountRequest da co.
Doc stocktake-test-scenarios.md cung thu muc truoc khi bat dau.
CANH BAO PHAM VI: hang cho xep va TOAN KHO gom ca du lieu CU trong cung kho.
Cac dot binh thuong KHONG bao gom hang cho xep, chi chua cac zone moi tao.
Chi duyet cac dong serial co tien to Tag cua bo test; khong duyet serial cu.
Khong tat trigger; moi loi se rollback toan bo seed.
*/
USE [WMSDB];
GO
IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Hay dong transaction hien tai truoc khi chay seed.',16,1);
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

DECLARE @Tag varchar(16)='QA261110';
DECLARE @StaffAEmail nvarchar(254)=N'phamttrang1025@gmail.com';
DECLARE @StaffBEmail nvarchar(254)=N'trangmooncat@gmail.com';
DECLARE @ManagerEmail nvarchar(254)=N'trangpham13061002@gmail.com';
DECLARE @OldActor sql_variant=SESSION_CONTEXT(N'ActorUserID'),@OldReason sql_variant=SESSION_CONTEXT(N'AuditReason');
DECLARE @Now datetime2(3)=SYSUTCDATETIME(),@Today date=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()));
DECLARE @A int,@B int,@Manager int,@Warehouse int,@Purchaser int,@Supplier int,@P1 int,@P2 int,@Lock int;
DECLARE @PO bigint,@Inbound bigint,@L1 bigint,@L2 bigint,@Count1 int,@Count2 int;
DECLARE @Length decimal(10,2),@Width decimal(10,2),@Weight decimal(10,2);
DECLARE @Case int,@Zone int,@RowA int,@RowB int,@G1 int,@G2 int,@D1 int,@GB int,@Tiny int,@Plan bigint;
DECLARE @Areas TABLE(CaseNo int PRIMARY KEY,ZoneID int,RowA int,RowB int,G1 int,G2 int,D1 int,GB int,Tiny int);
DECLARE @Plans TABLE(Code varchar(40),Id bigint,Description nvarchar(500));
DECLARE @Seed TABLE(Suffix varchar(20) PRIMARY KEY,CaseNo int,ProductID int,LocationKey varchar(8),ItemCondition varchar(8),Quarantined bit,Instruction nvarchar(500));
DECLARE @Sources TABLE(ProductID int,ItemCondition varchar(8),Id bigint,PRIMARY KEY(ProductID,ItemCondition));
BEGIN TRY
    IF @Tag IS NULL OR @Tag='' OR LEN(@Tag)>16 OR @Tag LIKE '%[^A-Za-z0-9]%' COLLATE Latin1_General_100_BIN2
        THROW 51400,N'Tag phai gom 1-16 chu/so, khong dau/khoang trang.',1;
    BEGIN TRANSACTION;
    EXEC @Lock=sys.sp_getapplock @Resource=N'EWMS.Stocktake.TestData.Extended',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
    IF @Lock<0 THROW 51401,N'Khong lay duoc khoa tao bo test.',1;
    IF COL_LENGTH(N'dbo.StocktakeAssignment',N'ScopeKind') IS NULL OR OBJECT_ID(N'dbo.StocktakeRecountRequest',N'U') IS NULL
        THROW 51402,N'Thieu ScopeKind/StocktakeRecountRequest. Khong thay doi schema trong seed nay.',1;
    IF EXISTS(SELECT 1 FROM dbo.SerialItem WHERE SerialNumber LIKE @Tag+'-%')
       OR EXISTS(SELECT 1 FROM dbo.Stocktake WHERE StocktakeCode LIKE 'ST-'+@Tag+'-%')
       OR EXISTS(SELECT 1 FROM dbo.StorageLocation WHERE LocationName LIKE @Tag+N' %')
       OR EXISTS(SELECT 1 FROM dbo.PurchaseOrder WHERE POCode='PO-'+@Tag)
       OR EXISTS(SELECT 1 FROM dbo.Inbound WHERE ReceiptCode='IN-'+@Tag)
        THROW 51403,N'Tag nay da co du lieu. Dung bo da tao hoac doi Tag; khong reset du lieu cu.',1;
    SELECT @A=u.UserID,@Warehouse=u.WarehouseID FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
        WHERE u.Email=@StaffAEmail AND u.Status='ACTIVE' AND r.RoleName='WAREHOUSE_STAFF';
    SELECT @B=u.UserID FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
        WHERE u.Email=@StaffBEmail AND u.Status='ACTIVE' AND r.RoleName='WAREHOUSE_STAFF' AND u.WarehouseID=@Warehouse;
    SELECT @Manager=u.UserID FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
        WHERE u.Email=@ManagerEmail AND u.Status='ACTIVE' AND r.RoleName='MANAGER';
    IF @A IS NULL OR @B IS NULL OR @A=@B OR @Manager IS NULL OR @Warehouse IS NULL
        THROW 51404,N'Kiem tra 3 email dau file: can 2 staff ACTIVE cung kho va Manager ACTIVE. Khong tu doi tai khoan.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID WHERE u.Status='ACTIVE' AND r.RoleName='CEO' AND NULLIF(LTRIM(RTRIM(u.Email)),N'') IS NOT NULL)
        THROW 51405,N'Can mot tai khoan CEO ACTIVE co Email de test duyet. Tao tren ung dung; seed khong tao mat khau.',1;
    SELECT @Warehouse=WarehouseID FROM dbo.Warehouse WITH(UPDLOCK,HOLDLOCK) WHERE WarehouseID=@Warehouse;
    IF NOT EXISTS(SELECT 1 FROM dbo.Warehouse WHERE WarehouseID=@Warehouse AND Status='ACTIVE'
        AND (WarehouseType='MAIN' OR @Today BETWEEN RentalStartDate AND RentalEndDate))
        THROW 51406,N'Kho cua staff khong hoat dong hoac khong con han thue.',1;
    IF EXISTS(SELECT 1 FROM dbo.Stocktake WHERE WarehouseID=@Warehouse AND ScopeType='WHOLE_WAREHOUSE'
        AND Status IN ('COUNTING','RECOUNTING','REVIEWING','PENDING_APPROVAL'))
        THROW 51407,N'Dang co dot khoa TOAN KHO. Hoan tat dot do truoc; seed khong tu sua dot cu.',1;
    IF EXISTS(SELECT 1 FROM dbo.Stocktake WHERE WarehouseID=@Warehouse AND IncludeAwaitingPutaway=1
        AND Status IN ('COUNTING','RECOUNTING','REVIEWING','PENDING_APPROVAL'))
        THROW 51408,N'Dang co dot khoa hang cho xep. Hoan tat dot do truoc khi them hang cho xep test.',1;
    SELECT TOP(1) @Purchaser=u.UserID FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
        WHERE u.Status='ACTIVE' AND r.RoleName='PURCHASING_STAFF' ORDER BY u.UserID;
    SELECT TOP(1) @Supplier=SupplierID FROM dbo.Supplier WHERE Status='ACTIVE' ORDER BY SupplierID;
    SELECT TOP(1) @P1=ProductID FROM dbo.Product WHERE Status='ACTIVE' AND LengthCm>0.02 AND WidthCm>0.02 AND HeightCm>0 AND GrossWeightKg>0.02 ORDER BY ProductID;
    SELECT TOP(1) @P2=ProductID FROM dbo.Product WHERE Status='ACTIVE' AND LengthCm>0 AND WidthCm>0 AND HeightCm>0 AND GrossWeightKg>0 AND ProductID<>@P1 ORDER BY ProductID;
    IF @Purchaser IS NULL OR @Supplier IS NULL OR @P1 IS NULL OR @P2 IS NULL
        THROW 51409,N'Can Purchasing Staff ACTIVE, Supplier ACTIVE va 2 san pham ACTIVE co kich thuoc/khoi luong hop le.',1;
    SELECT @Length=MAX(LengthCm)*20,@Width=MAX(WidthCm)*20,@Weight=MAX(GrossWeightKg)*50 FROM dbo.Product WHERE ProductID IN (@P1,@P2);
    EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=NULL;
    EXEC sys.sp_set_session_context @key=N'AuditReason',@value=N'Tao du lieu kiem thu kiem ke mo rong; khong sua du lieu cu';

    -- 4 zone doc lap. Row A co 2 o GOOD + 1 o DAMAGED; Row B co 1 o GOOD.
    SET @Case=1;
    WHILE @Case<=4
    BEGIN
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ZoneType,Status)
        VALUES(@Warehouse,NULL,@Tag+N' ZONE '+CONVERT(nvarchar(10),@Case),'ZONE','RESERVE','ACTIVE');
        SET @Zone=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,Status)
        VALUES(@Warehouse,@Zone,@Tag+N' Dãy A '+CONVERT(nvarchar(10),@Case),'ROW','FLOOR','ACTIVE'); SET @RowA=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,Status)
        VALUES(@Warehouse,@Zone,@Tag+N' Dãy B '+CONVERT(nvarchar(10),@Case),'ROW','FLOOR','ACTIVE'); SET @RowB=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
        VALUES(@Warehouse,@RowA,@Tag+N' G1-'+CONVERT(nvarchar(10),@Case),'FLOOR_SLOT','GOOD','FLOOR_AREA',@Length,@Width,@Weight,'ACTIVE'); SET @G1=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
        VALUES(@Warehouse,@RowA,@Tag+N' G2-'+CONVERT(nvarchar(10),@Case),'FLOOR_SLOT','GOOD','FLOOR_AREA',@Length,@Width,@Weight,'ACTIVE'); SET @G2=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
        VALUES(@Warehouse,@RowA,@Tag+N' D1-'+CONVERT(nvarchar(10),@Case),'FLOOR_SLOT','DAMAGED','FLOOR_AREA',@Length,@Width,@Weight,'ACTIVE'); SET @D1=CONVERT(int,SCOPE_IDENTITY());
        INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
        VALUES(@Warehouse,@RowB,@Tag+N' GB-'+CONVERT(nvarchar(10),@Case),'FLOOR_SLOT','GOOD','FLOOR_AREA',@Length,@Width,@Weight,'ACTIVE'); SET @GB=CONVERT(int,SCOPE_IDENTITY());
        SET @Tiny=NULL;
        IF @Case=4
        BEGIN
            -- Qua nho cho Product1, nhung cau truc vi tri van hop le; de test CEO chan ap dung.
            INSERT dbo.StorageLocation(WarehouseID,ParentLocationID,LocationName,LocationType,ConditionPolicy,CapacityMode,LengthCm,WidthCm,MaxWeightKg,Status)
            SELECT @Warehouse,@RowA,@Tag+N' TINY-4','FLOOR_SLOT','GOOD','FLOOR_AREA',LengthCm/2,WidthCm/2,GrossWeightKg/2,'ACTIVE' FROM dbo.Product WHERE ProductID=@P1;
            SET @Tiny=CONVERT(int,SCOPE_IDENTITY());
        END;
        INSERT @Areas VALUES(@Case,@Zone,@RowA,@RowB,@G1,@G2,@D1,@GB,@Tiny);
        SET @Case+=1;
    END;

    INSERT @Seed VALUES
    ('M01',1,@P1,'G1','GOOD',0,N'A: GOOD/G1-1; khop'),
    ('M02',1,@P2,'GB','GOOD',0,N'B: GOOD/GB-1; khop'),
    ('D01',2,@P1,'G1','GOOD',0,N'A: GOOD/G1-2; khop; dung test quet trung'),
    ('D02',2,@P1,'G1','GOOD',0,N'A: bo qua; MISSING sau khi dong vong'),
    ('D03',2,@P1,'G1','GOOD',0,N'A: GOOD/G2-2; WRONG_LOCATION'),
    ('D04',2,@P1,'G1','GOOD',0,N'A: DAMAGED/G1-2; CONDITION (CEO chan vi khu GOOD); dem lai DAMAGED/D1-2 de hop le'),
    ('D05',2,@P1,'G2','GOOD',0,N'A: DAMAGED/D1-2; COMBINED, co the ap dung'),
    ('D06',2,@P2,'G1','GOOD',0,N'A: GOOD/G1-2; khop, phai giu ket qua khi dem lai P1'),
    ('D07',2,@P2,'GB','GOOD',0,N'B: GOOD/GB-2; khop, khac khu vuc'),
    ('D08',2,@P1,'G1','GOOD',1,N'A: GOOD/G1-2; hang quarantine van phai dem'),
    ('D09',2,@P1,'D1','DAMAGED',1,N'A: DAMAGED/D1-2; hang hong/cach ly van nam trong ton vat ly'),
    ('D10',2,@P1,'GB','GOOD',0,N'B: GOOD/GB-2; khop, ngoai Row A'),
    ('W01',3,@P1,'G1','GOOD',0,N'A: GOOD/G1-3; co vi tri'),
    ('W02',3,@P1,'WAIT','GOOD',0,N'B: GOOD/Hang cho xep; khop'),
    ('W03',3,@P1,'WAIT','GOOD',0,N'B: bo qua; de test thieu + dem lai hang cho xep'),
    ('W04',3,@P2,'WAIT','DAMAGED',1,N'B: DAMAGED/Hang cho xep; khop'),
    ('N01',4,@P1,'G1','GOOD',0,N'A: GOOD/TINY-4 de test chan suc chua; dem lai GOOD/G1-4 de khoi phuc ket qua'),
    ('N02',4,@P2,'GB','GOOD',0,N'B: GOOD/GB-4; khop');

    INSERT dbo.SupplierProduct(SupplierID,ProductID)
    SELECT @Supplier,p.ProductID FROM dbo.Product p WHERE p.ProductID IN (@P1,@P2)
        AND NOT EXISTS(SELECT 1 FROM dbo.SupplierProduct x WHERE x.SupplierID=@Supplier AND x.ProductID=p.ProductID);
    SELECT @Count1=COUNT(*) FROM @Seed WHERE ProductID=@P1; SELECT @Count2=COUNT(*) FROM @Seed WHERE ProductID=@P2;
    INSERT dbo.PurchaseOrder(SupplierID,WarehouseID,CreatedByUserID,POCode,OrderDate,ExpectedArrivalDate,Status,Note,CreatedAt)
    VALUES(@Supplier,@Warehouse,@Purchaser,'PO-'+@Tag,@Today,@Today,'DRAFT',N'Chung tu rieng cho bo test kiem ke mo rong',@Now); SET @PO=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.PurchaseOrderLine(PurchaseOrderID,ProductID,OrderedQuantity,ClosedQuantity) VALUES(@PO,@P1,@Count1,0); SET @L1=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.PurchaseOrderLine(PurchaseOrderID,ProductID,OrderedQuantity,ClosedQuantity) VALUES(@PO,@P2,@Count2,0); SET @L2=CONVERT(bigint,SCOPE_IDENTITY());
    UPDATE dbo.PurchaseOrder SET Status='CONFIRMED',ConfirmedAt=@Now WHERE PurchaseOrderID=@PO;
    INSERT dbo.Inbound(PurchaseOrderID,CreatedByUserID,ReceiptCode,ReceiptDate,Status,CreatedAt)
    VALUES(@PO,@A,'IN-'+@Tag,@Today,'DRAFT',@Now); SET @Inbound=CONVERT(bigint,SCOPE_IDENTITY());
    DECLARE @Product int,@Condition varchar(8),@Source bigint;
    DECLARE SourceCursor CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT ProductID,ItemCondition FROM @Seed;
    OPEN SourceCursor; FETCH NEXT FROM SourceCursor INTO @Product,@Condition;
    WHILE @@FETCH_STATUS=0
    BEGIN
        INSERT dbo.SupplySource(ProductID,WarehouseID,ItemCondition,SourceType,SupplyState,InboundID,EligibleQuantity,RemainingQuantity,AvailableFrom,CreatedAt)
        SELECT @Product,@Warehouse,@Condition,'INBOUND','PHYSICAL',@Inbound,
            SUM(CASE WHEN Quarantined=0 AND ItemCondition='GOOD' THEN 1 ELSE 0 END),COUNT(*),@Today,@Now
        FROM @Seed WHERE ProductID=@Product AND ItemCondition=@Condition;
        SET @Source=CONVERT(bigint,SCOPE_IDENTITY()); INSERT @Sources VALUES(@Product,@Condition,@Source);
        FETCH NEXT FROM SourceCursor INTO @Product,@Condition;
    END;
    CLOSE SourceCursor; DEALLOCATE SourceCursor;
    INSERT dbo.SerialItem(ProductID,SerialNumber,OriginType,OriginalInboundID,CurrentWarehouseID,CurrentLocationID,CurrentSupplySourceID,CurrentSupplyState,StockState,ItemCondition,IsQuarantined,QuarantineReason,RegisteredAt)
    SELECT t.ProductID,@Tag+'-'+t.Suffix,'PURCHASE',@Inbound,@Warehouse,
        CASE t.LocationKey WHEN 'G1' THEN a.G1 WHEN 'G2' THEN a.G2 WHEN 'D1' THEN a.D1 WHEN 'GB' THEN a.GB ELSE NULL END,
        src.Id,'PHYSICAL','IN_WAREHOUSE',t.ItemCondition,t.Quarantined,CASE WHEN t.Quarantined=1 THEN N'Cach ly de kiem thu, khong duoc tu bo co nay khi kiem ke' ELSE NULL END,@Now
    FROM @Seed t JOIN @Areas a ON a.CaseNo=t.CaseNo JOIN @Sources src ON src.ProductID=t.ProductID AND src.ItemCondition=t.ItemCondition;
    INSERT dbo.InboundItem(InboundID,POLineID,PurchaseOrderID,ProductID,ScannedSerial,SerialItemID,ReceivedCondition,ConditionNote,ScannedAt,IsActive)
    SELECT @Inbound,CASE WHEN t.ProductID=@P1 THEN @L1 ELSE @L2 END,@PO,t.ProductID,s.SerialNumber,s.SerialItemID,t.ItemCondition,
        CASE WHEN t.ItemCondition='DAMAGED' THEN N'Hong trong bo test tiep nhan, chap nhan nhan hang va cach ly' ELSE NULL END,@Now,1
    FROM @Seed t JOIN dbo.SerialItem s ON s.SerialNumber=@Tag+'-'+t.Suffix;
    INSERT dbo.SerialMovement(SerialItemID,PerformedByUserID,EventType,OccurredAt,ToState,ToCondition,ToWarehouseID,ToLocationID,ToQuarantined,InboundID,Note)
    SELECT s.SerialItemID,@A,'PURCHASE_RECEIPT',@Now,s.StockState,s.ItemCondition,s.CurrentWarehouseID,s.CurrentLocationID,s.IsQuarantined,@Inbound,N'Ton dau cua bo test '+@Tag
    FROM @Seed t JOIN dbo.SerialItem s ON s.SerialNumber=@Tag+'-'+t.Suffix;
    UPDATE dbo.Inbound SET Status='DONE',PerformedByUserID=@A,CompletedAt=@Now WHERE InboundID=@Inbound;
    UPDATE dbo.PurchaseOrder SET Status='RECEIVED' WHERE PurchaseOrderID=@PO;

    SET @Case=1;
    WHILE @Case<=4
    BEGIN
        SELECT @Zone=ZoneID,@RowA=RowA,@RowB=RowB FROM @Areas WHERE CaseNo=@Case;
        INSERT dbo.Stocktake(WarehouseID,CreatedByUserID,StocktakeCode,ScopeType,Status,Note,CreatedAt,IncludeAwaitingPutaway,PlannedAt)
        VALUES(@Warehouse,@Manager,'ST-'+@Tag+'-0'+CONVERT(varchar(1),@Case),'LOCATIONS','PLANNED',
            CASE @Case WHEN 1 THEN N'Khop: A day A; B day B.' WHEN 2 THEN N'Chenh lech: A day A; B day B. Recount P1 day A giao B.'
            WHEN 3 THEN N'CO hang cho xep: A toan Zone 3; B tat ca hang cho xep cua kho (gom hang cu neu co).'
            ELSE N'Validate: A day A; B day B. N01 vao o TINY de test CEO chan suc chua.' END,@Now,CASE WHEN @Case=3 THEN 1 ELSE 0 END,@Now);
        SET @Plan=CONVERT(bigint,SCOPE_IDENTITY());
        INSERT dbo.StocktakeScope(StocktakeID,LocationID,WarehouseID) VALUES(@Plan,@Zone,@Warehouse);
        IF @Case=3
            INSERT dbo.StocktakeAssignment(StocktakeID,UserID,LocationID,Status,AssignedAt,WarehouseID,ScopeKind)
            VALUES(@Plan,@A,@Zone,'ASSIGNED',@Now,@Warehouse,'LOCATION'),(@Plan,@B,NULL,'ASSIGNED',@Now,@Warehouse,'AWAITING_PUTAWAY');
        ELSE
            INSERT dbo.StocktakeAssignment(StocktakeID,UserID,LocationID,Status,AssignedAt,WarehouseID,ScopeKind)
            VALUES(@Plan,@A,@RowA,'ASSIGNED',@Now,@Warehouse,'LOCATION'),(@Plan,@B,@RowB,'ASSIGNED',@Now,@Warehouse,'LOCATION');
        INSERT @Plans VALUES('ST-'+@Tag+'-0'+CONVERT(varchar(1),@Case),@Plan,CASE @Case WHEN 1 THEN N'Khop - lam dau tien' WHEN 2 THEN N'Chenh lech/recount/CEO' WHEN 3 THEN N'CO hang cho xep - lam sau NO-WAIT' ELSE N'Chan suc chua/tu choi/quet lai' END);
        SET @Case+=1;
    END;
    -- Cung Zone 3 nhung KHONG co hang cho xep. Chay truoc -03.
    SELECT @Zone=ZoneID FROM @Areas WHERE CaseNo=3;
    INSERT dbo.Stocktake(WarehouseID,CreatedByUserID,StocktakeCode,ScopeType,Status,Note,CreatedAt,IncludeAwaitingPutaway,PlannedAt)
    VALUES(@Warehouse,@Manager,'ST-'+@Tag+'-NO-WAIT','LOCATIONS','PLANNED',N'A toan Zone 3. Chi W01; W02/W03/W04 khong thuoc snapshot.',@Now,0,@Now); SET @Plan=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.StocktakeScope(StocktakeID,LocationID,WarehouseID) VALUES(@Plan,@Zone,@Warehouse);
    INSERT dbo.StocktakeAssignment(StocktakeID,UserID,LocationID,Status,AssignedAt,WarehouseID,ScopeKind) VALUES(@Plan,@A,NULL,'ASSIGNED',@Now,@Warehouse,'ALL_SCOPE');
    INSERT @Plans VALUES('ST-'+@Tag+'-NO-WAIT',@Plan,N'Lam TRUOC -03; A quet W01 GOOD/G1-3 roi gui; Manager hoan tat');
    -- Dot chong lan de test Start bi chan khi -02 dang COUNTING/REVIEWING.
    SELECT @Zone=ZoneID FROM @Areas WHERE CaseNo=2;
    INSERT dbo.Stocktake(WarehouseID,CreatedByUserID,StocktakeCode,ScopeType,Status,Note,CreatedAt,IncludeAwaitingPutaway,PlannedAt)
    VALUES(@Warehouse,@Manager,'ST-'+@Tag+'-OVERLAP','LOCATIONS','PLANNED',N'Chi thu Start khi -02 dang khoa: phai bi chan. De PLANNED, khong bat dau sau do neu chua can.',@Now,0,@Now); SET @Plan=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.StocktakeScope(StocktakeID,LocationID,WarehouseID) VALUES(@Plan,@Zone,@Warehouse);
    INSERT dbo.StocktakeAssignment(StocktakeID,UserID,LocationID,Status,AssignedAt,WarehouseID,ScopeKind) VALUES(@Plan,@A,NULL,'ASSIGNED',@Now,@Warehouse,'ALL_SCOPE');
    INSERT @Plans VALUES('ST-'+@Tag+'-OVERLAP',@Plan,N'Negative test: Start trong luc -02 chua hoan tat -> bi chan');
    INSERT dbo.Stocktake(WarehouseID,CreatedByUserID,StocktakeCode,ScopeType,Status,Note,CreatedAt,IncludeAwaitingPutaway,PlannedAt)
    VALUES(@Warehouse,@Manager,'ST-'+@Tag+'-WHOLE','WHOLE_WAREHOUSE','PLANNED',N'TOAN KHO gom hang cu. Chi Start cuoi cung khi cac dot khoa khac da hoan tat; khong duyet dong hang cu neu chi test.',@Now,1,@Now); SET @Plan=CONVERT(bigint,SCOPE_IDENTITY());
    INSERT dbo.StocktakeAssignment(StocktakeID,UserID,LocationID,Status,AssignedAt,WarehouseID,ScopeKind) VALUES(@Plan,@A,NULL,'ASSIGNED',@Now,@Warehouse,'ALL_SCOPE');
    INSERT @Plans VALUES('ST-'+@Tag+'-WHOLE',@Plan,N'FULL WAREHOUSE: gom tat ca ton cu + moi; chi test cuoi, hoac chi thu khoa chong lan va de PLANNED');

    -- Tu kiem tra bo seed moi tao; khong dua vao ID co dinh.
    IF (SELECT COUNT(*) FROM dbo.SerialItem WHERE SerialNumber LIKE @Tag+'-%')<>(SELECT COUNT(*) FROM @Seed)
        THROW 51410,N'So serial seed khong khop.',1;
    IF (SELECT COUNT(*) FROM dbo.SerialItem WHERE SerialNumber LIKE @Tag+'-%' AND CurrentLocationID IS NULL)<>3
        THROW 51411,N'Seed phai co 3 serial cho xep.',1;
    IF EXISTS(SELECT 1 FROM @Sources src JOIN dbo.SupplySource s ON s.SupplySourceID=src.Id
        WHERE s.RemainingQuantity<>(SELECT COUNT(*) FROM dbo.SerialItem i WHERE i.CurrentSupplySourceID=s.SupplySourceID AND i.StockState='IN_WAREHOUSE'))
        THROW 51412,N'So luong nguon khong khop serial vat ly.',1;
    COMMIT TRANSACTION;
    EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=@OldActor;
    EXEC sys.sp_set_session_context @key=N'AuditReason',@value=@OldReason;
    SELECT @Tag AS TestTag,w.WarehouseID,w.WarehouseName FROM dbo.Warehouse w WHERE w.WarehouseID=@Warehouse;
    SELECT Code,Id AS StocktakeID,Description FROM @Plans ORDER BY Code;
    SELECT r.RoleName,u.FullName,u.Email,u.UserID FROM dbo.[User] u JOIN dbo.[Role] r ON r.RoleID=u.RoleID
        WHERE u.UserID IN (@A,@B,@Manager) OR (r.RoleName='CEO' AND u.Status='ACTIVE');
    SELECT a.*,z.LocationName AS ZoneName FROM @Areas a JOIN dbo.StorageLocation z ON z.LocationID=a.ZoneID ORDER BY CaseNo;
    SELECT s.SerialNumber,p.ProductCode,p.ProductName,ISNULL(l.LocationName,N'HANG CHO XEP') AS InitialLocation,
        s.ItemCondition,s.IsQuarantined,t.Instruction
    FROM @Seed t JOIN dbo.SerialItem s ON s.SerialNumber=@Tag+'-'+t.Suffix JOIN dbo.Product p ON p.ProductID=t.ProductID
        LEFT JOIN dbo.StorageLocation l ON l.LocationID=s.CurrentLocationID ORDER BY t.CaseNo,t.Suffix;
    SELECT N'UNKNOWN - CHUA TAO SERIAL CHINH THUC, NHAP TREN UI' AS Note,@Tag+'-EXTRA01' AS Serial,@P1 AS ProductID;
    SELECT N'HANG CHO XEP CU: PHAI DEM CA CAC DONG NAY KHI TEST -03/WHOLE' AS Warning,
        s.SerialNumber,p.ProductName,s.ItemCondition,s.IsQuarantined
    FROM dbo.SerialItem s JOIN dbo.Product p ON p.ProductID=s.ProductID
    WHERE s.CurrentWarehouseID=@Warehouse AND s.StockState='IN_WAREHOUSE' AND s.CurrentLocationID IS NULL AND s.SerialNumber NOT LIKE @Tag+'-%';
    PRINT N'Da tao 7 ke hoach PLANNED va 18 serial; chua tao ket qua hay duyet. Doc file huong dan va test theo thu tu.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    EXEC sys.sp_set_session_context @key=N'ActorUserID',@value=@OldActor;
    EXEC sys.sp_set_session_context @key=N'AuditReason',@value=@OldReason;
    THROW;
END CATCH;
