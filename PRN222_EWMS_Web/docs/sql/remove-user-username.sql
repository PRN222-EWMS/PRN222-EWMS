-- Chạy trên WMSDB trong SSMS sau khi dừng ứng dụng và sao lưu database.
-- Phiên bản ứng dụng mới chỉ dùng Email để nhận diện tài khoản.
-- Các tài khoản mẫu chưa có email vẫn được giữ lại nhưng không thể đăng nhập.
USE [WMSDB];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.[User]', 'Username') IS NULL
    THROW 50001, 'Username column does not exist.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.[User]') AND name = 'UX_User_01')
    THROW 50002, 'UX_User_01 index does not exist.', 1;

-- Giữ nguyên việc ghi audit; bỏ thuộc tính Username khỏi JSON trước/sau.
EXEC(N'CREATE OR ALTER TRIGGER dbo.[TR_User_Audit] ON dbo.[User] AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    IF ROWCOUNT_BIG() = 0 RETURN;
    SET NOCOUNT ON;

    INSERT dbo.AuditLog(ActorUserID, DatabasePrincipal, TableName, EntityKey, ActionType,
                        BeforeData, AfterData, Reason, RequestID)
    SELECT TRY_CONVERT(int, SESSION_CONTEXT(N''ActorUserID'')), ORIGINAL_LOGIN(), N''User'',
           CONVERT(nvarchar(200), COALESCE(i.[UserID], d.[UserID])),
           CASE WHEN d.[UserID] IS NULL THEN ''INSERT''
                WHEN i.[UserID] IS NULL THEN ''DELETE'' ELSE ''UPDATE'' END,
           CASE WHEN d.[UserID] IS NOT NULL THEN
               (SELECT d.[UserID], d.[RoleID], d.[WarehouseID], d.[FullName], d.[Email],
                       d.[Phone], d.[Status], d.[CreatedAt]
                FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER) END,
           CASE WHEN i.[UserID] IS NOT NULL THEN
               (SELECT i.[UserID], i.[RoleID], i.[WarehouseID], i.[FullName], i.[Email],
                       i.[Phone], i.[Status], i.[CreatedAt]
                FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER) END,
           TRY_CONVERT(nvarchar(1000), SESSION_CONTEXT(N''AuditReason'')),
           TRY_CONVERT(nvarchar(100), SESSION_CONTEXT(N''RequestID''))
    FROM inserted i FULL OUTER JOIN deleted d ON i.[UserID] = d.[UserID];
END');

DROP INDEX [UX_User_01] ON dbo.[User];
ALTER TABLE dbo.[User] DROP CONSTRAINT [CK_User_NonBlank];
ALTER TABLE dbo.[User] DROP COLUMN [Username];

COMMIT TRANSACTION;
GO
