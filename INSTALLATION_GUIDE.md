# RemoteDesktopSystem 安裝與操作手冊

## 1. 目的

本手冊提供目前 `RemoteDesktopSystem` 的正式安裝、設定、啟動、發佈與日常操作方式。

目前系統為：
- `RemoteDesktop.Host`：Windows Forms 主控台
- `RemoteDesktop.Agent`：Windows Forms 被控端 Agent
- 通訊方式：Host 背景 `Kestrel` + WebSocket `/ws/agent`
- 資料庫：SQL Server / LocalDB

## 2. 系統架構

### 2.1 Host

`RemoteDesktop.Host` 提供：
- 管理者登入畫面
- 裝置清單
- 在線數量與連線紀錄
- 遠端畫面檢視與控制
- Host 設定表單
- 背景健康檢查 `/healthz`

### 2.2 Agent

`RemoteDesktop.Agent` 提供：
- 桌面擷取
- 心跳回報
- 滑鼠與鍵盤輸入回放
- 自動重連
- Agent 設定表單

## 3. 需求

- Windows 10 / Windows 11
- 如使用原始碼執行：`.NET 8 SDK`
- 如使用 `publish` 版本：不需另裝 .NET
- SQL Server LocalDB 或 SQL Server

## 4. 專案目錄

- `src/RemoteDesktop.Host`
- `src/RemoteDesktop.Agent`
- `deploy/publish/Host`
- `deploy/publish/Agent`
- `deploy/scripts`
- `tests/RemoteDesktop.SmokeTests`
- `tests/RemoteDesktop.UiAutomation`

## 5. 設定檔

### 5.1 Host

檔案：`deploy/publish/Host/appsettings.json`

主要欄位：
- `ConnectionStrings:RemoteDesktopDb`
- `ControlServer:ServerUrl`
- `ControlServer:ConsoleName`
- `ControlServer:AdminUserName`
- `ControlServer:AdminPassword`
- `ControlServer:SharedAccessKey`
- `ControlServer:AgentHeartbeatTimeoutSeconds`
- `ControlServer:RequireHttpsRedirect`

範例：

```json
{
  "ConnectionStrings": {
    "RemoteDesktopDb": "Server=(localdb)\\MSSQLLocalDB;Database=RemoteDesktopControl;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;"
  },
  "ControlServer": {
    "ServerUrl": "http://localhost:5106",
    "ConsoleName": "RemoteDesk Control",
    "AdminUserName": "admin",
    "AdminPassword": "ChangeThisStrongPassword!2026",
    "RequireHttpsRedirect": false,
    "SharedAccessKey": "ChangeThisAgentSharedKey!2026",
    "AgentHeartbeatTimeoutSeconds": 45
  }
}
```

### 5.2 Agent

檔案：`deploy/publish/Agent/appsettings.json`

主要欄位：
- `Agent:ServerUrl`
- `Agent:DeviceId`
- `Agent:DeviceName`
- `Agent:SharedAccessKey`
- `Agent:CaptureFramesPerSecond`
- `Agent:JpegQuality`
- `Agent:MaxFrameWidth`
- `Agent:ReconnectDelaySeconds`

範例：

```json
{
  "Agent": {
    "ServerUrl": "http://localhost:5106",
    "DeviceId": "pc-office-01",
    "DeviceName": "辦公室主機 01",
    "SharedAccessKey": "ChangeThisAgentSharedKey!2026",
    "CaptureFramesPerSecond": 8,
    "JpegQuality": 55,
    "MaxFrameWidth": 1600,
    "ReconnectDelaySeconds": 5
  }
}
```

## 6. 原始碼建置

```powershell
Set-Location 'G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408'
$env:DOTNET_CLI_HOME="$PWD\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
& 'C:\Program Files\dotnet\dotnet.exe' build .\RemoteDesktopSystem.sln
```

## 7. 產生 publish 版

目前已產出：
- `deploy/publish/Host`
- `deploy/publish/Agent`

若要重新產出：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' publish .\src\RemoteDesktop.Host\RemoteDesktop.Host.csproj -c Release -r win-x64 --self-contained true -o .\deploy\publish\Host
& 'C:\Program Files\dotnet\dotnet.exe' publish .\src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj -c Release -r win-x64 --self-contained true -o .\deploy\publish\Agent
```

## 8. 啟動方式

### 8.1 直接執行 publish 版

- Host：`deploy/publish/Host/RemoteDesktop.Host.exe`
- Agent：`deploy/publish/Agent/RemoteDesktop.Agent.exe`

### 8.2 使用啟動腳本

- `deploy/scripts/Start-Host.cmd`
- `deploy/scripts/Start-Agent.cmd`

### 8.3 使用桌面捷徑

已建立：
- `C:\Users\TECHUP\Desktop\RemoteDesktop Host.lnk`
- `C:\Users\TECHUP\Desktop\RemoteDesktop Agent.lnk`
- `C:\Users\TECHUP\Desktop\RemoteDesktop Host 設定資料夾.lnk`
- `C:\Users\TECHUP\Desktop\RemoteDesktop Agent 設定資料夾.lnk`

### 8.4 Agent 開機自動啟動

已建立：
- `C:\Users\TECHUP\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\RemoteDesktop Agent 開機啟動.lnk`

Windows 登入後，Agent 會自動啟動。

## 9. Host 操作流程

1. 啟動 `RemoteDesktop.Host.exe`
2. 使用登入畫面輸入：
   - 帳號：`ControlServer:AdminUserName`
   - 密碼：`ControlServer:AdminPassword`
3. 進入主畫面後可看到：
   - 裝置清單
   - 在線裝置數
   - Presence Log
   - 健康檢查位址
4. 按「設定」可修改 Host 參數
5. 雙擊在線裝置或按「開啟遠端畫面」即可開啟 Viewer

## 10. Agent 操作流程

1. 啟動 `RemoteDesktop.Agent.exe`
2. 主畫面會顯示：
   - 控制端 URL
   - DeviceId
   - DeviceName
   - 目前狀態
   - 最近連線時間
   - 最近送圖時間
   - 最近錯誤
   - 最近事件
3. 按「設定」可修改 Agent 參數
4. 修改後重新啟動 Agent 生效

## 11. 連線驗證

### 11.1 手動驗證

- Host 啟動後，可在主畫面看到 Agent 上線
- Agent 啟動後，狀態應顯示已連線
- Host 雙擊在線裝置可開啟遠端畫面

### 11.2 健康檢查

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5106/healthz
```

### 11.3 自動化驗證

核心連線 smoke test：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.SmokeTests\RemoteDesktop.SmokeTests.csproj
```

WinForms UI automation：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.UiAutomation\RemoteDesktop.UiAutomation.csproj
```

## 12. 常見問題

### 12.1 Host 啟動顯示找不到 `ConnectionStrings:RemoteDesktopDb`

原因：
- 執行目錄沒有 `appsettings.json`

目前已修正：
- `publish` 與 `build` 輸出都會自動帶上 `appsettings.json`

### 12.2 Agent 無法連上 Host

請檢查：
- Host 是否已啟動
- `Agent:ServerUrl` 是否正確
- `SharedAccessKey` 是否與 Host 完全一致
- 防火牆是否允許對應埠

### 12.3 資料庫初始化失敗

請檢查：
- `ConnectionStrings:RemoteDesktopDb` 是否可連線
- SQL Server/LocalDB 是否存在
- 帳號權限是否足夠

## 13. 建議交付方式

如果要交給操作人員，建議直接提供：
- `deploy/publish/Host`
- `deploy/publish/Agent`
- 本手冊 `INSTALLATION_GUIDE.md`
- 桌面捷徑

## 14. 已驗證結果

- `dotnet build .\RemoteDesktopSystem.sln`：成功
- `tests/RemoteDesktop.SmokeTests`：成功
- `tests/RemoteDesktop.UiAutomation`：成功
- Host/Agent `appsettings.json` 已確認會輸出到執行目錄
