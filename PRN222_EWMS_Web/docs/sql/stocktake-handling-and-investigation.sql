/*
EWMS - XU LY VI TRI/HANG HONG VA DOI SOAT SERIAL LA - 10/11/2026
CHAY TOAN BO FILE trong SSMS tren WMSDB (doi USE neu nhom dung ten khac).
File chi tao 2 bang moi, rang buoc/index va trigger audit tren bang moi.
KHONG ALTER bang cu, KHONG seed, KHONG sua/xoa serial, ton, chung tu hay tai khoan.
Can schema 49 bang + ScopeKind + StocktakeRecountRequest da cai dat.
Su dung migration nay TRUOC khi chay code xu ly kiem ke moi.
Chay lai cung migration bo qua; schema cu/khac marker se dung de kiem tra.
Application van phai kiem role/kho/pham vi/RowVersion, capacity, picking,
nguon hang/reservation, dong bo serial/movement va transaction.
CEO chi duyet thieu/thua. Bang Handling khong tu dang mot dieu chinh tang/giam ton.
StocktakeInvestigation lien ket mot dot WHOLE_WAREHOUSE doc lap, khong doi snapshot goc.
Chua thuc thi file tren database cua ban; nhom tu chay va chia se file len Drive.
*/
USE [WMSDB];
GO
IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Hay dong transaction hien tai truoc khi chay migration.',16,1);
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
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Lock int;
    EXEC @Lock=sys.sp_getapplock @Resource=N'EWMS.StocktakeHandlingInvestigation.Install.v1',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
    IF @Lock<0 THROW 51500,N'Khong lay duoc khoa cai dat.',1;
    IF OBJECT_ID(N'dbo.StocktakeRecountRequest',N'U') IS NULL OR COL_LENGTH(N'dbo.StocktakeAssignment',N'ScopeKind') IS NULL
       OR OBJECT_ID(N'dbo.AuditLog',N'U') IS NULL OR OBJECT_ID(N'dbo.SerialMovement',N'U') IS NULL
        THROW 51501,N'Thieu schema 49 bang, ScopeKind hoac StocktakeRecountRequest.',1;
    IF OBJECT_ID(N'dbo.StocktakeHandling',N'U') IS NOT NULL OR OBJECT_ID(N'dbo.StocktakeInvestigation',N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.StocktakeHandling',N'U') IS NULL OR OBJECT_ID(N'dbo.StocktakeInvestigation',N'U') IS NULL
           OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'dbo.StocktakeHandling') AND minor_id=0 AND name=N'EWMS_MigrationVersion' AND CONVERT(nvarchar(100),value)=N'StocktakeHandlingInvestigation.v1')
           OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'dbo.StocktakeInvestigation') AND minor_id=0 AND name=N'EWMS_MigrationVersion' AND CONVERT(nvarchar(100),value)=N'StocktakeHandlingInvestigation.v1')
            THROW 51502,N'Bang handling/investigation da co nhung khong dung phien ban migration. Khong tu ghi de.',1;
        COMMIT TRANSACTION;
        PRINT N'StocktakeHandlingInvestigation.v1 da cai dat. Khong thay doi du lieu.';
        RETURN;
    END;

    CREATE TABLE dbo.StocktakeHandling
    (
        HandlingID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StocktakeHandling PRIMARY KEY,
        StocktakeID bigint NOT NULL,
        ObservationID bigint NOT NULL,
        SerialItemID bigint NOT NULL,
        ScannedSerial nvarchar(100) NOT NULL,
        ProductID int NOT NULL,
        FoundLocationID int NULL,
        OriginalLocationID int NULL,
        TargetLocationID int NULL,
        ActualCondition varchar(8) NOT NULL,
        Status varchar(16) NOT NULL CONSTRAINT DF_StocktakeHandling_Status DEFAULT('WAITING'),
        RecordedByUserID int NOT NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_StocktakeHandling_CreatedAt DEFAULT(SYSUTCDATETIME()),
        MovedByUserID int NULL,
        MovedAt datetime2(3) NULL,
        MovementID bigint NULL,
        Note nvarchar(1000) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_StocktakeHandling_Stocktake FOREIGN KEY(StocktakeID) REFERENCES dbo.Stocktake(StocktakeID),
        CONSTRAINT FK_StocktakeHandling_Observation FOREIGN KEY(ObservationID) REFERENCES dbo.StocktakeObservation(ObservationID),
        CONSTRAINT FK_StocktakeHandling_Serial FOREIGN KEY(SerialItemID) REFERENCES dbo.SerialItem(SerialItemID),
        CONSTRAINT FK_StocktakeHandling_Product FOREIGN KEY(ProductID) REFERENCES dbo.Product(ProductID),
        CONSTRAINT FK_StocktakeHandling_Found FOREIGN KEY(FoundLocationID) REFERENCES dbo.StorageLocation(LocationID),
        CONSTRAINT FK_StocktakeHandling_Original FOREIGN KEY(OriginalLocationID) REFERENCES dbo.StorageLocation(LocationID),
        CONSTRAINT FK_StocktakeHandling_Target FOREIGN KEY(TargetLocationID) REFERENCES dbo.StorageLocation(LocationID),
        CONSTRAINT FK_StocktakeHandling_RecordedBy FOREIGN KEY(RecordedByUserID) REFERENCES dbo.[User](UserID),
        CONSTRAINT FK_StocktakeHandling_MovedBy FOREIGN KEY(MovedByUserID) REFERENCES dbo.[User](UserID),
        CONSTRAINT FK_StocktakeHandling_Movement FOREIGN KEY(MovementID) REFERENCES dbo.SerialMovement(MovementID),
        CONSTRAINT CK_StocktakeHandling_Condition CHECK(ActualCondition IN ('GOOD','DAMAGED')),
        CONSTRAINT CK_StocktakeHandling_State CHECK(
            (Status IN ('WAITING','CANCELLED') AND MovedByUserID IS NULL AND MovedAt IS NULL AND MovementID IS NULL)
            OR (Status='MOVED' AND MovedByUserID IS NOT NULL AND MovedAt IS NOT NULL AND MovementID IS NOT NULL
                AND TargetLocationID IS NOT NULL)),
        CONSTRAINT CK_StocktakeHandling_Time CHECK(MovedAt IS NULL OR MovedAt>=CreatedAt)
    );
    CREATE UNIQUE INDEX UX_StocktakeHandling_Serial ON dbo.StocktakeHandling(StocktakeID,ScannedSerial);
    CREATE UNIQUE INDEX UX_StocktakeHandling_Movement ON dbo.StocktakeHandling(MovementID) WHERE MovementID IS NOT NULL;
    CREATE INDEX IX_StocktakeHandling_Status ON dbo.StocktakeHandling(StocktakeID,Status,SerialItemID);

    CREATE TABLE dbo.StocktakeInvestigation
    (
        StocktakeInvestigationID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StocktakeInvestigation PRIMARY KEY,
        StocktakeID bigint NOT NULL,
        ObservationID bigint NOT NULL,
        InvestigationStocktakeID bigint NOT NULL,
        RequestedByUserID int NOT NULL,
        RequestedAt datetime2(3) NOT NULL CONSTRAINT DF_StocktakeInvestigation_RequestedAt DEFAULT(SYSUTCDATETIME()),
        ClosedAt datetime2(3) NULL,
        ClosedByUserID int NULL,
        Outcome varchar(24) NULL,
        Conclusion nvarchar(1000) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_StocktakeInvestigation_Parent FOREIGN KEY(StocktakeID) REFERENCES dbo.Stocktake(StocktakeID),
        CONSTRAINT FK_StocktakeInvestigation_Observation FOREIGN KEY(ObservationID) REFERENCES dbo.StocktakeObservation(ObservationID),
        CONSTRAINT FK_StocktakeInvestigation_Child FOREIGN KEY(InvestigationStocktakeID) REFERENCES dbo.Stocktake(StocktakeID),
        CONSTRAINT FK_StocktakeInvestigation_RequestedBy FOREIGN KEY(RequestedByUserID) REFERENCES dbo.[User](UserID),
        CONSTRAINT FK_StocktakeInvestigation_ClosedBy FOREIGN KEY(ClosedByUserID) REFERENCES dbo.[User](UserID),
        CONSTRAINT CK_StocktakeInvestigation_Separate CHECK(StocktakeID<>InvestigationStocktakeID),
        CONSTRAINT CK_StocktakeInvestigation_State CHECK(
            (ClosedAt IS NULL AND ClosedByUserID IS NULL AND Outcome IS NULL AND Conclusion IS NULL)
            OR (ClosedAt IS NOT NULL AND ClosedByUserID IS NOT NULL AND Outcome IS NOT NULL AND Outcome IN ('CONFIRMED_SURPLUS','NEEDS_REVIEW')
                AND NULLIF(LTRIM(RTRIM(Conclusion)),N'') IS NOT NULL AND ClosedAt>=RequestedAt))
    );
    CREATE UNIQUE INDEX UX_StocktakeInvestigation_Child ON dbo.StocktakeInvestigation(InvestigationStocktakeID);
    CREATE UNIQUE INDEX UX_StocktakeInvestigation_OpenEvidence ON dbo.StocktakeInvestigation(StocktakeID,ObservationID) WHERE ClosedAt IS NULL;
    CREATE INDEX IX_StocktakeInvestigation_Parent ON dbo.StocktakeInvestigation(StocktakeID,ClosedAt,Outcome);

    EXEC(N'CREATE TRIGGER dbo.TR_StocktakeHandling_Links ON dbo.StocktakeHandling AFTER INSERT,UPDATE,DELETE AS
    BEGIN
        IF ROWCOUNT_BIG()=0 RETURN;
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.HandlingID=d.HandlingID WHERE i.HandlingID IS NULL)
            THROW 51503,N''Keep stocktake handling evidence; do not delete it.'',1;
        IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.HandlingID=d.HandlingID WHERE d.Status=''MOVED'')
            THROW 51504,N''Confirmed stocktake handling is immutable.'',1;
        IF EXISTS(SELECT 1 FROM inserted i
            JOIN dbo.Stocktake p ON p.StocktakeID=i.StocktakeID
            JOIN dbo.StocktakeObservation o ON o.ObservationID=i.ObservationID
            JOIN dbo.SerialItem s ON s.SerialItemID=i.SerialItemID
            LEFT JOIN dbo.StorageLocation f ON f.LocationID=i.FoundLocationID
            LEFT JOIN dbo.StorageLocation oldLoc ON oldLoc.LocationID=i.OriginalLocationID
            LEFT JOIN dbo.StorageLocation t ON t.LocationID=i.TargetLocationID
            LEFT JOIN dbo.SerialMovement m ON m.MovementID=i.MovementID
            WHERE o.StocktakeID<>i.StocktakeID OR o.ScannedSerial<>i.ScannedSerial OR o.ProductID IS NULL OR o.ProductID<>i.ProductID
                OR o.SerialItemID IS NULL OR o.SerialItemID<>i.SerialItemID OR o.ActualQuantity<>1
                OR (i.Status<>''CANCELLED'' AND (o.ActualCondition IS NULL OR o.ActualCondition<>i.ActualCondition OR ISNULL(o.ActualLocationID,-1)<>ISNULL(i.FoundLocationID,-1)))
                OR (o.ExpectedQuantity=1 AND ISNULL(o.ExpectedLocationID,-1)<>ISNULL(i.OriginalLocationID,-1))
                OR s.SerialNumber<>i.ScannedSerial OR s.ProductID<>i.ProductID OR s.StockState<>''IN_WAREHOUSE'' OR s.CurrentWarehouseID IS NULL OR s.CurrentWarehouseID<>p.WarehouseID
                OR (f.LocationID IS NOT NULL AND f.WarehouseID<>p.WarehouseID)
                OR (oldLoc.LocationID IS NOT NULL AND oldLoc.WarehouseID<>p.WarehouseID)
                OR (t.LocationID IS NOT NULL AND (t.WarehouseID<>p.WarehouseID OR t.LocationType NOT IN (''TIER'',''FLOOR_SLOT'')))
                OR (i.Status=''MOVED'' AND (m.SerialItemID<>i.SerialItemID OR m.EventType<>''RELOCATION''
                    OR m.PerformedByUserID<>i.MovedByUserID OR m.ToState<>''IN_WAREHOUSE'' OR m.ToWarehouseID<>p.WarehouseID
                    OR m.ToCondition<>i.ActualCondition OR ISNULL(m.ToLocationID,-1)<>ISNULL(i.TargetLocationID,-1))))
            THROW 51505,N''Handling evidence, serial, warehouse or movement does not match.'',1;
    END;');
    EXEC(N'CREATE TRIGGER dbo.TR_StocktakeInvestigation_Links ON dbo.StocktakeInvestigation AFTER INSERT,UPDATE,DELETE AS
    BEGIN
        IF ROWCOUNT_BIG()=0 RETURN;
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.StocktakeInvestigationID=d.StocktakeInvestigationID WHERE i.StocktakeInvestigationID IS NULL)
            THROW 51506,N''Keep investigation links and conclusions.'',1;
        IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.StocktakeInvestigationID=d.StocktakeInvestigationID
            WHERE d.ClosedAt IS NOT NULL OR i.StocktakeID<>d.StocktakeID OR i.ObservationID<>d.ObservationID
                OR i.InvestigationStocktakeID<>d.InvestigationStocktakeID OR i.RequestedByUserID<>d.RequestedByUserID OR i.RequestedAt<>d.RequestedAt)
            THROW 51507,N''Investigation identity and closed conclusions are immutable.'',1;
        IF EXISTS(SELECT 1 FROM inserted i
            JOIN dbo.Stocktake p ON p.StocktakeID=i.StocktakeID
            JOIN dbo.Stocktake child ON child.StocktakeID=i.InvestigationStocktakeID
            JOIN dbo.StocktakeObservation o ON o.ObservationID=i.ObservationID
            WHERE p.WarehouseID<>child.WarehouseID OR o.StocktakeID<>p.StocktakeID
                OR o.ExpectedQuantity<>0 OR o.ActualQuantity<>1 OR o.SerialItemID IS NOT NULL
                OR child.ScopeType<>''WHOLE_WAREHOUSE'' OR child.IncludeAwaitingPutaway<>1)
            THROW 51508,N''Investigation must link an unknown serial to a separate whole-warehouse count in the same warehouse.'',1;
    END;');

    EXEC(N'CREATE TRIGGER dbo.TR_StocktakeHandling_Audit ON dbo.StocktakeHandling AFTER INSERT,UPDATE AS
    BEGIN
        IF ROWCOUNT_BIG()=0 RETURN;
        SET NOCOUNT ON;
        INSERT dbo.AuditLog(ActorUserID,DatabasePrincipal,TableName,EntityKey,ActionType,BeforeData,AfterData,Reason,RequestID)
        SELECT TRY_CONVERT(int,SESSION_CONTEXT(N''ActorUserID'')),ORIGINAL_LOGIN(),N''StocktakeHandling'',CONVERT(nvarchar(200),i.HandlingID),
            CASE WHEN d.HandlingID IS NULL THEN ''INSERT'' ELSE ''UPDATE'' END,
            CASE WHEN d.HandlingID IS NOT NULL THEN (SELECT d.HandlingID,d.StocktakeID,d.ObservationID,d.SerialItemID,d.ScannedSerial,d.ProductID,d.FoundLocationID,d.OriginalLocationID,d.TargetLocationID,d.ActualCondition,d.Status,d.RecordedByUserID,d.CreatedAt,d.MovedByUserID,d.MovedAt,d.MovementID,d.Note FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
            (SELECT i.HandlingID,i.StocktakeID,i.ObservationID,i.SerialItemID,i.ScannedSerial,i.ProductID,i.FoundLocationID,i.OriginalLocationID,i.TargetLocationID,i.ActualCondition,i.Status,i.RecordedByUserID,i.CreatedAt,i.MovedByUserID,i.MovedAt,i.MovementID,i.Note FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
            CONVERT(nvarchar(1000),SESSION_CONTEXT(N''AuditReason'')),CONVERT(nvarchar(100),SESSION_CONTEXT(N''RequestID''))
        FROM inserted i LEFT JOIN deleted d ON d.HandlingID=i.HandlingID;
    END;');
    EXEC(N'CREATE TRIGGER dbo.TR_StocktakeInvestigation_Audit ON dbo.StocktakeInvestigation AFTER INSERT,UPDATE AS
    BEGIN
        IF ROWCOUNT_BIG()=0 RETURN;
        SET NOCOUNT ON;
        INSERT dbo.AuditLog(ActorUserID,DatabasePrincipal,TableName,EntityKey,ActionType,BeforeData,AfterData,Reason,RequestID)
        SELECT TRY_CONVERT(int,SESSION_CONTEXT(N''ActorUserID'')),ORIGINAL_LOGIN(),N''StocktakeInvestigation'',CONVERT(nvarchar(200),i.StocktakeInvestigationID),
            CASE WHEN d.StocktakeInvestigationID IS NULL THEN ''INSERT'' ELSE ''UPDATE'' END,
            CASE WHEN d.StocktakeInvestigationID IS NOT NULL THEN (SELECT d.StocktakeInvestigationID,d.StocktakeID,d.ObservationID,d.InvestigationStocktakeID,d.RequestedByUserID,d.RequestedAt,d.ClosedAt,d.ClosedByUserID,d.Outcome,d.Conclusion FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
            (SELECT i.StocktakeInvestigationID,i.StocktakeID,i.ObservationID,i.InvestigationStocktakeID,i.RequestedByUserID,i.RequestedAt,i.ClosedAt,i.ClosedByUserID,i.Outcome,i.Conclusion FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
            CONVERT(nvarchar(1000),SESSION_CONTEXT(N''AuditReason'')),CONVERT(nvarchar(100),SESSION_CONTEXT(N''RequestID''))
        FROM inserted i LEFT JOIN deleted d ON d.StocktakeInvestigationID=i.StocktakeInvestigationID;
    END;');

    EXEC sys.sp_addextendedproperty @name=N'EWMS_MigrationVersion',@value=N'StocktakeHandlingInvestigation.v1',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'StocktakeHandling';
    EXEC sys.sp_addextendedproperty @name=N'EWMS_MigrationVersion',@value=N'StocktakeHandlingInvestigation.v1',@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'StocktakeInvestigation';
    COMMIT TRANSACTION;
    PRINT N'Da tao StocktakeHandling + StocktakeInvestigation. Khong chen/sua du lieu nghiep vu.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT DB_NAME() AS DatabaseName,t.name AS NewTable
FROM sys.tables t WHERE t.name IN ('StocktakeHandling','StocktakeInvestigation') ORDER BY t.name;
