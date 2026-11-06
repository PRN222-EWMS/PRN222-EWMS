-- Run against the application's database. Existing duplicates must be resolved first.
-- This script does not modify or delete existing account data.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (
    SELECT Phone FROM dbo.[User]
    WHERE Phone IS NOT NULL
    GROUP BY Phone HAVING COUNT(*) > 1
)
BEGIN
    SELECT Phone, COUNT(*) AS AccountCount FROM dbo.[User]
    WHERE Phone IS NOT NULL
    GROUP BY Phone HAVING COUNT(*) > 1;
    ROLLBACK TRANSACTION;
    THROW 50001, 'Resolve duplicate phone numbers before creating UX_User_Phone.', 1;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.[User]') AND name = 'UX_User_Phone'
)
    CREATE UNIQUE INDEX UX_User_Phone ON dbo.[User](Phone) WHERE Phone IS NOT NULL;

COMMIT TRANSACTION;
