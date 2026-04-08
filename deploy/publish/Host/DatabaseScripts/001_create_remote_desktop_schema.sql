IF OBJECT_ID(N'dbo.RemoteDesktopDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RemoteDesktopDevices
    (
        DeviceId NVARCHAR(64) NOT NULL CONSTRAINT PK_RemoteDesktopDevices PRIMARY KEY,
        DeviceName NVARCHAR(128) NOT NULL,
        HostName NVARCHAR(128) NOT NULL,
        AgentVersion NVARCHAR(32) NOT NULL,
        ScreenWidth INT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_ScreenWidth DEFAULT (0),
        ScreenHeight INT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_ScreenHeight DEFAULT (0),
        IsOnline BIT NOT NULL CONSTRAINT DF_RemoteDesktopDevices_IsOnline DEFAULT (0),
        CreatedAt DATETIMEOFFSET(0) NOT NULL,
        UpdatedAt DATETIMEOFFSET(0) NOT NULL,
        LastSeenAt DATETIMEOFFSET(0) NOT NULL,
        LastConnectedAt DATETIMEOFFSET(0) NULL,
        LastDisconnectedAt DATETIMEOFFSET(0) NULL
    );
END;

IF OBJECT_ID(N'dbo.RemoteDesktopAgentPresenceLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RemoteDesktopAgentPresenceLogs
    (
        PresenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RemoteDesktopAgentPresenceLogs PRIMARY KEY,
        DeviceId NVARCHAR(64) NOT NULL,
        DeviceName NVARCHAR(128) NOT NULL,
        HostName NVARCHAR(128) NOT NULL,
        AgentVersion NVARCHAR(32) NOT NULL,
        ConnectedAt DATETIMEOFFSET(0) NOT NULL,
        LastSeenAt DATETIMEOFFSET(0) NOT NULL,
        DisconnectedAt DATETIMEOFFSET(0) NULL,
        DisconnectReason NVARCHAR(64) NULL
    );
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
