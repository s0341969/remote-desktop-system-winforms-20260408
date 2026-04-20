IF OBJECT_ID(N'dbo.RemoteDesktopDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RemoteDesktopDevices
    (
        DeviceId NVARCHAR(64) NOT NULL CONSTRAINT PK_RemoteDesktopDevices PRIMARY KEY,
        DeviceName NVARCHAR(128) NOT NULL,
        HostName NVARCHAR(128) NOT NULL,
        RemoteIpAddress NVARCHAR(64) NULL,
        AgentVersion NVARCHAR(32) NOT NULL,
        ScreenWidth INT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_ScreenWidth DEFAULT (0),
        ScreenHeight INT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_ScreenHeight DEFAULT (0),
        InventoryJson NVARCHAR(MAX) NULL,
        InventoryCollectedAt DATETIMEOFFSET(0) NULL,
        IsOnline BIT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_IsOnline DEFAULT (0),
        IsAuthorized BIT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_IsAuthorized DEFAULT (0),
        AuthorizedAt DATETIMEOFFSET(0) NULL,
        AuthorizedBy NVARCHAR(128) NULL,
        CreatedAt DATETIMEOFFSET(0) NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSeenAt DATETIMEOFFSET(0) NOT NULL,
        LastConnectedAt DATETIMEOFFSET(0) NULL,
        LastDisconnectedAt DATETIMEOFFSET(0) NULL
    );
END;

IF COL_LENGTH(N'dbo.RemoteDesktopDevices', N'InventoryJson') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopDevices
    ADD InventoryJson NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'dbo.RemoteDesktopDevices', N'InventoryCollectedAt') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopDevices
    ADD InventoryCollectedAt DATETIMEOFFSET(0) NULL;
END;

IF COL_LENGTH(N'dbo.RemoteDesktopDevices', N'RemoteIpAddress') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopDevices
    ADD RemoteIpAddress NVARCHAR(64) NULL;
END;

IF OBJECT_ID(N'dbo.RemoteDesktopInventoryHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RemoteDesktopInventoryHistory
    (
        HistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RemoteDesktopInventoryHistory PRIMARY KEY,
        DeviceId NVARCHAR(64) NOT NULL,
        InventoryFingerprint NVARCHAR(64) NOT NULL,
        InventoryJson NVARCHAR(MAX) NOT NULL,
        CollectedAt DATETIMEOFFSET(0) NOT NULL,
        RecordedAt DATETIMEOFFSET(0) NOT NULL,
        ChangeSummary NVARCHAR(512) NULL
    );
END;

IF COL_LENGTH(N'dbo.RemoteDesktopDevices', N'IsAuthorized') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopDevices
    ADD IsAuthorized BIT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_IsAuthorized_Compat DEFAULT (0);
END;

IF COL_LENGTH(N'dbo.RemoteDesktopInventoryHistory', N'InventoryFingerprint') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopInventoryHistory
    ADD InventoryFingerprint NVARCHAR(64) NOT NULL CONSTRAINT DF_RemoteDesktopInventoryHistory_InventoryFingerprint DEFAULT (N'');
END;

IF COL_LENGTH(N'dbo.RemoteDesktopInventoryHistory', N'ChangeSummary') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopInventoryHistory
    ADD ChangeSummary NVARCHAR(512) NULL;
END;

IF COL_LENGTH(N'dbo.RemoteDesktopDevices', N'AuthorizedAt') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopDevices
    ADD AuthorizedAt DATETIMEOFFSET(0) NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_RemoteDesktopInventoryHistory_DeviceId'
      AND object_id = OBJECT_ID(N'dbo.RemoteDesktopInventoryHistory')
)
BEGIN
    CREATE INDEX IX_RemoteDesktopInventoryHistory_DeviceId
        ON dbo.RemoteDesktopInventoryHistory (DeviceId, RecordedAt DESC);
END;

IF COL_LENGTH(N'dbo.RemoteDesktopDevices', N'AuthorizedBy') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopDevices
    ADD AuthorizedBy NVARCHAR(128) NULL;
END;

IF OBJECT_ID(N'dbo.RemoteDesktopAgentPresenceLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RemoteDesktopAgentPresenceLogs
    (
        PresenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RemoteDesktopAgentPresenceLogs PRIMARY KEY,
        DeviceId NVARCHAR(64) NOT NULL,
        DeviceName NVARCHAR(128) NOT NULL,
        HostName NVARCHAR(128) NOT NULL,
        RemoteIpAddress NVARCHAR(64) NULL,
        AgentVersion NVARCHAR(32) NOT NULL,
        ConnectedAt DATETIMEOFFSET(0) NOT NULL,
        LastSeenAt DATETIMEOFFSET(0) NOT NULL,
        DisconnectedAt DATETIMEOFFSET(0) NULL,
        DisconnectReason NVARCHAR(64) NULL
    );
END;

IF COL_LENGTH(N'dbo.RemoteDesktopAgentPresenceLogs', N'RemoteIpAddress') IS NULL
BEGIN
    ALTER TABLE dbo.RemoteDesktopAgentPresenceLogs
    ADD RemoteIpAddress NVARCHAR(64) NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_RemoteDesktopDevices_Online'
      AND object_id = OBJECT_ID(N'dbo.RemoteDesktopDevices')
)
BEGIN
    CREATE INDEX IX_RemoteDesktopDevices_Online
        ON dbo.RemoteDesktopDevices (IsOnline, LastSeenAt DESC);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_RemoteDesktopAgentPresenceLogs_DeviceId'
      AND object_id = OBJECT_ID(N'dbo.RemoteDesktopAgentPresenceLogs')
)
BEGIN
    CREATE INDEX IX_RemoteDesktopAgentPresenceLogs_DeviceId
        ON dbo.RemoteDesktopAgentPresenceLogs (DeviceId, ConnectedAt DESC);
END;

UPDATE dbo.RemoteDesktopDevices
SET IsOnline = 0;

UPDATE dbo.RemoteDesktopAgentPresenceLogs
SET
    DisconnectedAt = COALESCE(DisconnectedAt, SYSDATETIMEOFFSET()),
    DisconnectReason = COALESCE(DisconnectReason, 'server-restart')
WHERE DisconnectedAt IS NULL;
