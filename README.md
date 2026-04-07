# RemoteDesktopSystem

`RemoteDesktopSystem` 是一套以 `.NET 8` 建置的遠端桌面原型系統，採用 `Control Server + Windows Agent` 架構。

## 系統組成

- `RemoteDesktop.Host`
  - ASP.NET Core Razor Pages 控制台
  - 提供管理者登入、裝置清單、上下線紀錄、遠端檢視頁面
  - 透過 WebSocket 轉送 Agent 畫面與 Viewer 操作命令
  - 使用 MSSQL 紀錄裝置狀態與 Presence Log
- `RemoteDesktop.Agent`
  - 執行於 Windows 被控端
  - 擷取桌面畫面並壓縮為 JPEG
  - 接收 Viewer 的滑鼠與鍵盤輸入命令

## 目前行為

- Host 啟動時會自動執行 `src/RemoteDesktop.Host/DatabaseScripts/001_create_remote_desktop_schema.sql`
- Agent 啟動後會以 `SharedAccessKey` 向 Host 驗證並註冊
- Viewer 頁面只允許已登入的管理者開啟
- Host 提供健康檢查端點 `/healthz`
- 開發模式下 Host 預設 URL 來自 `launchSettings.json`
  - `http://localhost:5106`
  - `https://localhost:7242`

## 快速開始

1. 安裝 `.NET 8 SDK`
2. 準備 `MSSQL` 或 `SQL Server LocalDB`
3. 設定：
   - `src/RemoteDesktop.Host/appsettings.json`
   - `src/RemoteDesktop.Agent/appsettings.json`
4. 在專案根目錄執行：

```powershell
$env:DOTNET_CLI_HOME="$PWD\\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
& 'C:\Program Files\dotnet\dotnet.exe' build
```

5. 啟動 Host：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Host\RemoteDesktop.Host.csproj
```

6. 啟動 Agent：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj
```

## 文件

- 完整安裝、設定與操作步驟請見 `INSTALLATION_GUIDE.md`

## 重要設定

### Host

檔案：`src/RemoteDesktop.Host/appsettings.json`

- `ConnectionStrings:RemoteDesktopDb`
  - MSSQL 連線字串
- `ControlServer:AdminUserName`
  - 控制台登入帳號
- `ControlServer:AdminPassword`
  - 控制台登入密碼
- `ControlServer:SharedAccessKey`
  - Agent 註冊共用金鑰
- `ControlServer:RequireHttpsRedirect`
  - 是否強制 HTTPS
- `ControlServer:AgentHeartbeatTimeoutSeconds`
  - Agent 心跳逾時秒數

### Agent

檔案：`src/RemoteDesktop.Agent/appsettings.json`

- `Agent:ServerUrl`
  - Host 對外服務 URL
- `Agent:DeviceId`
  - 裝置唯一識別值
- `Agent:DeviceName`
  - 控制台顯示名稱
- `Agent:SharedAccessKey`
  - 必須與 Host 相同
- `Agent:CaptureFramesPerSecond`
  - 畫面擷取 FPS，範圍 `1-24`
- `Agent:JpegQuality`
  - JPEG 品質，範圍 `30-90`
- `Agent:MaxFrameWidth`
  - 傳輸畫面最大寬度，範圍 `640-3840`
- `Agent:ReconnectDelaySeconds`
  - 斷線後重連秒數，範圍 `1-60`

## 專案目錄

- `RemoteDesktopSystem.csproj`
  - 聚合建置入口
- `src/RemoteDesktop.Host`
  - 控制台網站
- `src/RemoteDesktop.Agent`
  - Windows Agent

## 已知限制

- 目前為單一管理者帳號密碼模式
- Agent 尚未封裝為 Windows Service
- 專案內部分 UI 檔案仍有舊編碼遺留亂碼
