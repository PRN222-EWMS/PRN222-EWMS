-- Run once on WMSDB before enabling the new inbound workflow.
IF COL_LENGTH('dbo.InboundItem', 'CaptureMethod') IS NULL
    ALTER TABLE dbo.InboundItem ADD CaptureMethod varchar(20) NULL;
IF COL_LENGTH('dbo.InboundItem', 'ScannedByUserID') IS NULL
    ALTER TABLE dbo.InboundItem ADD ScannedByUserID int NULL;
IF COL_LENGTH('dbo.Inbound', 'CancelReason') IS NULL
    ALTER TABLE dbo.Inbound ADD CancelReason nvarchar(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Inbound') AND name = 'UX_Inbound_OneDraftPerOrder')
    CREATE UNIQUE INDEX UX_Inbound_OneDraftPerOrder ON dbo.Inbound(PurchaseOrderID) WHERE Status = 'DRAFT';
