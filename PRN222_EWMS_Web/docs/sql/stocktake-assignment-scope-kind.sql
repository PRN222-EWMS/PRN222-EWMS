/*
    EWMS / SQL Server 2022 / WMSDB
    Run this entire file in SSMS. This file is NOT executed by the application.

    Adds dbo.StocktakeAssignment.ScopeKind:
      LOCATION          = LocationID and its descendants within the plan scope.
      AWAITING_PUTAWAY  = only awaiting-putaway stock included in the plan.
      ALL_SCOPE         = the entire PLAN scope, which may be a partial warehouse.

    Existing rows keep their meaning:
      LocationID IS NOT NULL -> LOCATION; LocationID IS NULL -> ALL_SCOPE.
    No existing row is automatically assigned to AWAITING_PUTAWAY.

    Adds a CHECK and a filtered unique index; preserves existing FKs/indexes.
    Patches only the two audit JSON projections to include ScopeKind.
    No warehouse, location, serial, stock balance, snapshot or plan is created/deleted.

    No DEFAULT is added: new assignment INSERTs must explicitly supply ScopeKind.
    The MVC service must also enforce same-warehouse active staff, disjoint/full
    coverage, ALL_SCOPE exclusivity, IncludeAwaitingPutaway, and pre-Start edits.
    Deleting an unstarted plan does not require another column or table.
*/

USE [WMSDB];
GO

-- RAISERROR does not abort the caller's transaction, even if XACT_ABORT is ON.
IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR(N'Run this file without an existing open transaction.', 16, 1);
    RETURN;
END;

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @PreviousAuditReason sql_variant = SESSION_CONTEXT(N'AuditReason');
DECLARE @AuditReasonChanged bit = 0;

BEGIN TRY
    IF DB_NAME() <> N'WMSDB'
        THROW 51201, N'Wrong database. This script must run on WMSDB.', 1;

    IF OBJECT_ID(N'dbo.StocktakeAssignment', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Stocktake', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
       OR COL_LENGTH(N'dbo.StocktakeAssignment', N'LocationID') IS NULL
       OR COL_LENGTH(N'dbo.Stocktake', N'IncludeAwaitingPutaway') IS NULL
        THROW 51202, N'The required EWMS stocktake schema is missing. Nothing was changed.', 1;

    BEGIN TRANSACTION;

    -- Keep assignments stable while the column, backfill and constraints change.
    DECLARE @AssignmentCount bigint;
    SELECT @AssignmentCount = COUNT_BIG(*)
    FROM dbo.StocktakeAssignment WITH (TABLOCKX, HOLDLOCK);

    IF COL_LENGTH(N'dbo.StocktakeAssignment', N'ScopeKind') IS NULL
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.StocktakeAssignment ADD ScopeKind varchar(16) NULL;';

    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.StocktakeAssignment')
          AND c.name = N'ScopeKind'
          AND c.system_type_id = TYPE_ID(N'varchar')
          AND c.max_length = 16
          AND c.is_computed = 0
          AND c.default_object_id = 0
    )
        THROW 51203, N'ScopeKind must be varchar(16), not computed and without a default. Nothing was committed.', 1;

    -- Preserve the existing trigger body instead of replacing teammates' logic.
    DECLARE @AuditTriggerId int = OBJECT_ID(N'dbo.TR_StocktakeAssignment_Audit', N'TR');
    DECLARE @AuditSql nvarchar(max) = OBJECT_DEFINITION(@AuditTriggerId);

    IF @AuditSql IS NULL OR NOT EXISTS (
        SELECT 1 FROM sys.triggers
        WHERE object_id = @AuditTriggerId
          AND parent_id = OBJECT_ID(N'dbo.StocktakeAssignment')
          AND is_disabled = 0
    )
        THROW 51204, N'The expected enabled assignment audit trigger is unavailable. Nothing was committed.', 1;

    DECLARE @PatchAudit bit = 0;

    IF CHARINDEX(N'd.[WarehouseID],d.[ScopeKind] FOR JSON PATH', @AuditSql) = 0
    BEGIN
        IF CHARINDEX(N'd.[WarehouseID] FOR JSON PATH', @AuditSql) = 0
            THROW 51205, N'Unexpected BeforeData audit definition. Review it before migrating; nothing was committed.', 1;

        SET @AuditSql = REPLACE(@AuditSql,
            N'd.[WarehouseID] FOR JSON PATH',
            N'd.[WarehouseID],d.[ScopeKind] FOR JSON PATH');
        SET @PatchAudit = 1;
    END;

    IF CHARINDEX(N'i.[WarehouseID],i.[ScopeKind] FOR JSON PATH', @AuditSql) = 0
    BEGIN
        IF CHARINDEX(N'i.[WarehouseID] FOR JSON PATH', @AuditSql) = 0
            THROW 51206, N'Unexpected AfterData audit definition. Review it before migrating; nothing was committed.', 1;

        SET @AuditSql = REPLACE(@AuditSql,
            N'i.[WarehouseID] FOR JSON PATH',
            N'i.[WarehouseID],i.[ScopeKind] FOR JSON PATH');
        SET @PatchAudit = 1;
    END;

    IF @PatchAudit = 1
    BEGIN
        SET @AuditSql = LTRIM(@AuditSql);

        IF LEFT(UPPER(@AuditSql), LEN(N'CREATE OR ALTER TRIGGER')) = N'CREATE OR ALTER TRIGGER'
            SET @AuditSql = STUFF(@AuditSql, 1, LEN(N'CREATE OR ALTER TRIGGER'), N'ALTER TRIGGER');
        ELSE IF LEFT(UPPER(@AuditSql), LEN(N'CREATE TRIGGER')) = N'CREATE TRIGGER'
            SET @AuditSql = STUFF(@AuditSql, 1, LEN(N'CREATE TRIGGER'), N'ALTER TRIGGER');
        ELSE IF LEFT(UPPER(@AuditSql), LEN(N'ALTER TRIGGER')) <> N'ALTER TRIGGER'
            THROW 51207, N'Unexpected audit trigger header. Nothing was committed.', 1;

        -- ALTER TRIGGER must be the first statement in its own batch.
        EXEC sys.sp_executesql @AuditSql;
    END;

    EXEC sys.sp_set_session_context
        @key = N'AuditReason',
        @value = N'Migration: classify legacy stocktake assignments without changing their scope.';
    SET @AuditReasonChanged = 1;

    -- Compile references to the new column only AFTER ALTER TABLE has completed.
    EXEC sys.sp_executesql N'
        UPDATE dbo.StocktakeAssignment
        SET ScopeKind = CASE WHEN LocationID IS NULL THEN ''ALL_SCOPE'' ELSE ''LOCATION'' END
        WHERE ScopeKind IS NULL;

        IF EXISTS (
            SELECT 1 FROM dbo.StocktakeAssignment
            WHERE ScopeKind IS NULL
               OR NOT (
                    (ScopeKind = ''LOCATION'' AND LocationID IS NOT NULL)
                    OR (ScopeKind IN (''ALL_SCOPE'', ''AWAITING_PUTAWAY'') AND LocationID IS NULL)
               )
        )
            THROW 51208, N''Invalid ScopeKind/LocationID combination. Nothing was committed.'', 1;

        IF EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
              AND name = N''ScopeKind'' AND is_nullable = 1
        )
            ALTER TABLE dbo.StocktakeAssignment ALTER COLUMN ScopeKind varchar(16) NOT NULL;

        IF NOT EXISTS (
            SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
              AND name = N''CK_StocktakeAssignment_ScopeKind''
        )
            ALTER TABLE dbo.StocktakeAssignment WITH CHECK
            ADD CONSTRAINT CK_StocktakeAssignment_ScopeKind CHECK (
                (ScopeKind = ''LOCATION'' AND LocationID IS NOT NULL)
                OR (ScopeKind = ''ALL_SCOPE'' AND LocationID IS NULL)
                OR (ScopeKind = ''AWAITING_PUTAWAY'' AND LocationID IS NULL)
            );

        -- Refuse a same-name constraint with a different definition on a rerun.
        DECLARE @CheckDefinition nvarchar(max);
        SELECT @CheckDefinition = UPPER(definition)
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
          AND name = N''CK_StocktakeAssignment_ScopeKind'';
        SET @CheckDefinition = REPLACE(TRANSLATE(@CheckDefinition,
            N''[]'' + NCHAR(9) + NCHAR(10) + NCHAR(13), REPLICATE(N'' '', 5)), N'' '', N'''');

        IF @CheckDefinition IS NULL OR @CheckDefinition NOT IN (
            N''(SCOPEKIND=''''LOCATION''''ANDLOCATIONIDISNOTNULLORSCOPEKIND=''''ALL_SCOPE''''ANDLOCATIONIDISNULLORSCOPEKIND=''''AWAITING_PUTAWAY''''ANDLOCATIONIDISNULL)'',
            N''((SCOPEKIND=''''LOCATION''''ANDLOCATIONIDISNOTNULL)OR(SCOPEKIND=''''ALL_SCOPE''''ANDLOCATIONIDISNULL)OR(SCOPEKIND=''''AWAITING_PUTAWAY''''ANDLOCATIONIDISNULL))''
        )
            THROW 51210, N''Unexpected existing ScopeKind CHECK definition. Nothing was committed.'', 1;

        ALTER TABLE dbo.StocktakeAssignment WITH CHECK
            CHECK CONSTRAINT CK_StocktakeAssignment_ScopeKind;

        IF EXISTS (
            SELECT StocktakeID FROM dbo.StocktakeAssignment
            WHERE ScopeKind = ''AWAITING_PUTAWAY'' AND Status <> ''CANCELLED''
            GROUP BY StocktakeID HAVING COUNT_BIG(*) > 1
        )
            THROW 51209, N''A plan has multiple effective awaiting-putaway assignments. Nothing was committed.'', 1;

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
              AND name = N''UX_StocktakeAssignment_OneAwaitingPutaway''
        )
            CREATE UNIQUE INDEX UX_StocktakeAssignment_OneAwaitingPutaway
            ON dbo.StocktakeAssignment (StocktakeID)
            WHERE ScopeKind = ''AWAITING_PUTAWAY'' AND Status <> ''CANCELLED'';

        DECLARE @AwaitingIndexId int, @IndexFilter nvarchar(max);
        SELECT @AwaitingIndexId = index_id, @IndexFilter = UPPER(filter_definition)
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
          AND name = N''UX_StocktakeAssignment_OneAwaitingPutaway''
          AND is_unique = 1 AND has_filter = 1 AND is_disabled = 0
          AND is_hypothetical = 0 AND type = 2 AND ignore_dup_key = 0;
        SET @IndexFilter = REPLACE(TRANSLATE(@IndexFilter,
            N''[]()'' + NCHAR(9) + NCHAR(10) + NCHAR(13), REPLICATE(N'' '', 7)), N'' '', N'''');

        IF @AwaitingIndexId IS NULL
           OR @IndexFilter IS NULL
           OR @IndexFilter <> N''SCOPEKIND=''''AWAITING_PUTAWAY''''ANDSTATUS<>''''CANCELLED''''''
           OR (SELECT COUNT(*) FROM sys.index_columns
               WHERE object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
                 AND index_id = @AwaitingIndexId AND key_ordinal > 0) <> 1
           OR NOT EXISTS (
               SELECT 1 FROM sys.index_columns ic
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE ic.object_id = OBJECT_ID(N''dbo.StocktakeAssignment'')
                 AND ic.index_id = @AwaitingIndexId AND ic.key_ordinal = 1
                 AND c.name = N''StocktakeID''
           )
            THROW 51211, N''Unexpected existing awaiting-putaway index definition. Nothing was committed.'', 1;
    ';

    EXEC sys.sp_set_session_context @key = N'AuditReason', @value = @PreviousAuditReason;
    SET @AuditReasonChanged = 0;

    COMMIT TRANSACTION;

    PRINT N'Completed: StocktakeAssignment.ScopeKind, legacy backfill, CHECK, unique awaiting assignment and audit.';
    PRINT N'New assignment INSERTs must explicitly provide ScopeKind. No inventory or snapshots were changed.';

    EXEC sys.sp_executesql N'
        SELECT ScopeKind, COUNT_BIG(*) AS AssignmentCount
        FROM dbo.StocktakeAssignment
        GROUP BY ScopeKind
        ORDER BY ScopeKind;';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    IF @AuditReasonChanged = 1
        EXEC sys.sp_set_session_context @key = N'AuditReason', @value = @PreviousAuditReason;

    THROW;
END CATCH;
