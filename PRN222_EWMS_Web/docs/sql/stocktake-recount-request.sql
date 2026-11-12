/*
EWMS - Yeu cau dem lai kiem ke - 08/11/2026

CACH CHAY:
  1. Script danh cho database WMSDB. Neu nhom dung ten khac, doi dong USE.
  2. Chay TOAN BO file trong SSMS, khong co transaction dang mo.
  3. Can schema 49 bang va StocktakeAssignment.ScopeKind (file cap nhat 16).

THAY DOI:
  - Them 1 bang dbo.StocktakeRecountRequest, FK/CHECK/index va 2 trigger
    chi tren bang moi (kiem quan he va ghi AuditLog).
  - Khong ALTER bang cu; khong INSERT du lieu mau; khong sua/xoa du lieu cu.
  - Khong doi ton kho, serial, phan cong ban dau hay snapshot.
  - Transaction rollback neu tao that bai. Chay lai bo qua bang da tao
    boi dung phien ban script nay; schema khac se bao loi de kiem tra.

NGHIEP VU DA CHOT:
  - Mot yeu cau = mot ProductID + mot khu vuc (gom vi tri con), hoac
    hang cho xep ke; giao mot nhan vien khac nguoi dem truoc, cung kho.
  - REQUESTED -> IN_PROGRESS -> SUBMITTED; CANCELLED chi truoc bat dau.
  - SourceRoundID la vong DA DONG duoc Manager ra soat.
  - ResultRoundID chi gan khi nhan vien bat dau; ket qua van nam trong
    StocktakeRound / StocktakeObservation, doi chieu snapshot GOC.
  - StocktakeRound hien chi cho 1 vong OPEN / dot kiem ke: cac yeu cau
    co the cho cung luc, nhung bat dau/tien hanh dem lai lan luot.
  - Service phai kiem role/kho/nhan vien ACTIVE tai thoi diem thao tac,
    nguoi dem lai khac nguoi truoc, pham vi thuoc ke hoach, khong chong
    pham vi yeu cau dang mo, va state machine trong transaction.
  - SUBMITTED chi la gui ket qua dem lai, KHONG phai CEO duyet dieu chinh.
  - Bang co trigger: EF Core can UseSqlOutputClause(false) khi mapping.

File chi duoc tao de nhom xem va tu chay; chua kiem thu tren SQL Server.
*/
USE [WMSDB];
GO

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Hay chay file trong cua so khong co transaction dang mo.', 16, 1);
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
    -- Serialize repeat installation without modifying any business table.
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'EWMS.StocktakeRecountRequest.Install.v1',
        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
    IF @LockResult < 0
        THROW 51200, N'Khong lay duoc khoa cai dat. Thu lai sau.', 1;

    IF OBJECT_ID(N'dbo.Stocktake', N'U') IS NULL
       OR OBJECT_ID(N'dbo.StocktakeRound', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Product', N'U') IS NULL
       OR OBJECT_ID(N'dbo.StorageLocation', N'U') IS NULL
       OR OBJECT_ID(N'dbo.User', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
       OR COL_LENGTH(N'dbo.StocktakeAssignment', N'ScopeKind') IS NULL
        THROW 51201, N'Thieu schema 49 bang hoac ScopeKind. Kiem tra database va file cap nhat 16.', 1;

    IF OBJECT_ID(N'dbo.StocktakeRecountRequest', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM sys.extended_properties
            WHERE class = 1 AND major_id = OBJECT_ID(N'dbo.StocktakeRecountRequest')
              AND minor_id = 0 AND name = N'EWMS_MigrationVersion'
              AND CONVERT(nvarchar(100), value) = N'StocktakeRecountRequest.v1'
        )
            THROW 51202, N'Bang recount da ton tai nhung khong dung dau phien ban. Khong tu ghi de.', 1;
        COMMIT TRANSACTION;
        PRINT N'Da cai dat StocktakeRecountRequest.v1 truoc do. Khong thay doi du lieu.';
        RETURN;
    END;

    CREATE TABLE dbo.StocktakeRecountRequest
    (
        RecountRequestID bigint IDENTITY(1,1) NOT NULL,
        StocktakeID bigint NOT NULL,
        SourceRoundID bigint NOT NULL,
        ProductID int NOT NULL,
        ScopeKind varchar(16) NOT NULL,
        LocationID int NULL,
        RequestedByUserID int NOT NULL,
        AssignedToUserID int NOT NULL,
        Reason nvarchar(1000) NOT NULL,
        Status varchar(16) NOT NULL
            CONSTRAINT DF_StocktakeRecountRequest_Status DEFAULT ('REQUESTED'),
        ResultRoundID bigint NULL,
        RequestedAt datetime2(3) NOT NULL
            CONSTRAINT DF_StocktakeRecountRequest_RequestedAt DEFAULT (SYSUTCDATETIME()),
        StartedAt datetime2(3) NULL,
        SubmittedAt datetime2(3) NULL,
        CancelledAt datetime2(3) NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_StocktakeRecountRequest PRIMARY KEY (RecountRequestID),
        CONSTRAINT FK_StocktakeRecountRequest_Stocktake FOREIGN KEY (StocktakeID)
            REFERENCES dbo.Stocktake(StocktakeID),
        CONSTRAINT FK_StocktakeRecountRequest_SourceRound FOREIGN KEY (SourceRoundID)
            REFERENCES dbo.StocktakeRound(RoundID),
        CONSTRAINT FK_StocktakeRecountRequest_ResultRound FOREIGN KEY (ResultRoundID)
            REFERENCES dbo.StocktakeRound(RoundID),
        CONSTRAINT FK_StocktakeRecountRequest_Product FOREIGN KEY (ProductID)
            REFERENCES dbo.Product(ProductID),
        CONSTRAINT FK_StocktakeRecountRequest_Location FOREIGN KEY (LocationID)
            REFERENCES dbo.StorageLocation(LocationID),
        CONSTRAINT FK_StocktakeRecountRequest_Requester FOREIGN KEY (RequestedByUserID)
            REFERENCES dbo.[User](UserID),
        CONSTRAINT FK_StocktakeRecountRequest_Assignee FOREIGN KEY (AssignedToUserID)
            REFERENCES dbo.[User](UserID),
        CONSTRAINT CK_StocktakeRecountRequest_Scope CHECK (
            (ScopeKind = 'LOCATION' AND LocationID IS NOT NULL)
            OR (ScopeKind = 'AWAITING_PUTAWAY' AND LocationID IS NULL)),
        CONSTRAINT CK_StocktakeRecountRequest_Reason CHECK (LEN(LTRIM(RTRIM(Reason))) > 0),
        CONSTRAINT CK_StocktakeRecountRequest_Lifecycle CHECK (
            (Status = 'REQUESTED' AND ResultRoundID IS NULL AND StartedAt IS NULL
                AND SubmittedAt IS NULL AND CancelledAt IS NULL)
            OR (Status = 'IN_PROGRESS' AND ResultRoundID IS NOT NULL AND StartedAt IS NOT NULL
                AND SubmittedAt IS NULL AND CancelledAt IS NULL)
            OR (Status = 'SUBMITTED' AND ResultRoundID IS NOT NULL AND StartedAt IS NOT NULL
                AND SubmittedAt IS NOT NULL AND CancelledAt IS NULL)
            OR (Status = 'CANCELLED' AND ResultRoundID IS NULL AND StartedAt IS NULL
                AND SubmittedAt IS NULL AND CancelledAt IS NOT NULL)),
        CONSTRAINT CK_StocktakeRecountRequest_Times CHECK (
            (StartedAt IS NULL OR StartedAt >= RequestedAt)
            AND (SubmittedAt IS NULL OR SubmittedAt >= StartedAt)
            AND (CancelledAt IS NULL OR CancelledAt >= RequestedAt)),
        CONSTRAINT CK_StocktakeRecountRequest_DifferentRound CHECK (
            ResultRoundID IS NULL OR ResultRoundID <> SourceRoundID)
    );

    CREATE INDEX IX_StocktakeRecountRequest_Assignee
        ON dbo.StocktakeRecountRequest(AssignedToUserID, Status, StocktakeID);
    CREATE INDEX IX_StocktakeRecountRequest_Scope
        ON dbo.StocktakeRecountRequest(StocktakeID, ProductID, ScopeKind, LocationID, Status);
    CREATE UNIQUE INDEX UX_StocktakeRecountRequest_ResultRound
        ON dbo.StocktakeRecountRequest(ResultRoundID) WHERE ResultRoundID IS NOT NULL;

    -- Validate cross-table links without adding keys/columns to existing tables.
    EXEC(N'CREATE TRIGGER dbo.TR_StocktakeRecountRequest_Links
    ON dbo.StocktakeRecountRequest AFTER INSERT, UPDATE, DELETE
    AS
    BEGIN
        IF ROWCOUNT_BIG() = 0 RETURN;
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i
            ON i.RecountRequestID=d.RecountRequestID WHERE i.RecountRequestID IS NULL)
            THROW 51203, N''Keep recount history; cancel an unstarted request instead of deleting.'', 1;
        IF EXISTS (SELECT 1 FROM inserted i
            JOIN dbo.Stocktake s ON s.StocktakeID=i.StocktakeID
            JOIN dbo.StocktakeRound src ON src.RoundID=i.SourceRoundID
            LEFT JOIN dbo.StocktakeRound dest ON dest.RoundID=i.ResultRoundID
            LEFT JOIN dbo.StorageLocation loc ON loc.LocationID=i.LocationID
            WHERE src.StocktakeID<>i.StocktakeID OR src.Status<>''CLOSED''
               OR (i.ResultRoundID IS NOT NULL AND (dest.StocktakeID<>i.StocktakeID
                   OR dest.RoundNumber<=src.RoundNumber))
               OR (i.LocationID IS NOT NULL AND loc.WarehouseID<>s.WarehouseID)
               OR (i.ScopeKind=''AWAITING_PUTAWAY'' AND s.ScopeType<>''WHOLE_WAREHOUSE''
                   AND s.IncludeAwaitingPutaway=0))
            THROW 51204, N''Recount round, warehouse or awaiting-putaway scope is invalid.'', 1;
    END;');

    EXEC(N'CREATE TRIGGER dbo.TR_StocktakeRecountRequest_Audit
    ON dbo.StocktakeRecountRequest AFTER INSERT, UPDATE
    AS
    BEGIN
        IF ROWCOUNT_BIG()=0 RETURN;
        SET NOCOUNT ON;
        INSERT dbo.AuditLog(ActorUserID,DatabasePrincipal,TableName,EntityKey,
            ActionType,BeforeData,AfterData,Reason,RequestID)
        SELECT TRY_CONVERT(int,SESSION_CONTEXT(N''ActorUserID'')), ORIGINAL_LOGIN(),
            N''StocktakeRecountRequest'', CONVERT(nvarchar(200),i.RecountRequestID),
            CASE WHEN d.RecountRequestID IS NULL THEN ''INSERT'' ELSE ''UPDATE'' END,
            CASE WHEN d.RecountRequestID IS NOT NULL THEN
                (SELECT d.RecountRequestID,d.StocktakeID,d.SourceRoundID,d.ProductID,
                    d.ScopeKind,d.LocationID,d.RequestedByUserID,d.AssignedToUserID,
                    d.Reason,d.Status,d.ResultRoundID,d.RequestedAt,d.StartedAt,
                    d.SubmittedAt,d.CancelledAt
                 FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
            (SELECT i.RecountRequestID,i.StocktakeID,i.SourceRoundID,i.ProductID,
                i.ScopeKind,i.LocationID,i.RequestedByUserID,i.AssignedToUserID,
                i.Reason,i.Status,i.ResultRoundID,i.RequestedAt,i.StartedAt,
                i.SubmittedAt,i.CancelledAt
             FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
            CONVERT(nvarchar(1000),SESSION_CONTEXT(N''AuditReason'')),
            CONVERT(nvarchar(100),SESSION_CONTEXT(N''RequestID''))
        FROM inserted i LEFT JOIN deleted d ON d.RecountRequestID=i.RecountRequestID;
    END;');

    EXEC sys.sp_addextendedproperty @name=N'EWMS_MigrationVersion',
        @value=N'StocktakeRecountRequest.v1',
        @level0type=N'SCHEMA', @level0name=N'dbo',
        @level1type=N'TABLE', @level1name=N'StocktakeRecountRequest';

    COMMIT TRANSACTION;
    PRINT N'Thanh cong: da them StocktakeRecountRequest va cac rang buoc/index/trigger.';
    PRINT N'Khong chen du lieu mau; khong thay doi ton kho hay bang cu.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Expected: one row, no business data created by this script.
SELECT DB_NAME() AS DatabaseName, t.name AS NewTable,
       (SELECT COUNT_BIG(*) FROM dbo.StocktakeRecountRequest) AS RequestCount
FROM sys.tables t WHERE t.object_id = OBJECT_ID(N'dbo.StocktakeRecountRequest');
