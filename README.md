# RemoteDesktopSystem

`RemoteDesktopSystem` 目前已重構為可直接使用 `Visual Studio 2022` 開啟的 Windows Forms 解決方案，保留原本的遠端桌面核心通訊邏輯，並把控制端與 Agent 都改成可由 WinForms 設計器編修的桌面應用程式。

## 目前架構

- `RemoteDesktopSystem.sln`
  - 給 `VS2022` 直接開啟的解決方案檔。
- `src/RemoteDesktop.Host`
  - Windows Forms 主控台。
  - 背景自架 `Kestrel`，提供 Agent 連線所需的 `/ws/agent` 與 `/healthz`。
  - 前景提供登入畫面、裝置清單、連線紀錄與遠端檢視視窗。
- `src/RemoteDesktop.Agent`
  - Windows Forms Agent。
  - 保留桌面截圖、心跳、輸入回放與自動重連。
  - 顯示目前狀態、最近連線、最近送圖與錯誤資訊。
- `RemoteDesktopSystem.csproj`
  - 根目錄聚合建置檔，用來一次建置 Host 與 Agent。

## 本次重構重點

- 控制端從 Razor Pages 改為 Windows Forms 主控台。
- 新增可由 VS2022 設計器直接編修的表單：
  - `src/RemoteDesktop.Host/Forms/LoginForm`
  - `src/RemoteDesktop.Host/Forms/MainForm`
  - `src/RemoteDesktop.Host/Forms/RemoteViewerForm`
  - `src/RemoteDesktop.Agent/Forms/AgentMainForm`
- 保留既有 MSSQL 裝置與 Presence Log 結構，避免破壞既有資料模型。
- 保留 Agent 與 Host 的 WebSocket 協定，避免重寫通訊層造成相容性風險。
- 新增 `RemoteDesktopSystem.sln`，可直接從 VS2022 載入整個專案。
- Host 已不再依賴瀏覽器操作；控制、監看與遠端畫面都在 WinForms 內完成。

## 使用 Visual Studio 2022

1. 開啟 `RemoteDesktopSystem.sln`
2. 在方案總管中選擇要編修的表單
3. 開啟下列檔案即可用設計器拖拉 UI 控制項：
   - `src/RemoteDesktop.Host/Forms/LoginForm.cs`
   - `src/RemoteDesktop.Host/Forms/MainForm.cs`
   - `src/RemoteDesktop.Host/Forms/RemoteViewerForm.cs`
   - `src/RemoteDesktop.Agent/Forms/AgentMainForm.cs`
4. 需要啟動控制端時，將 `RemoteDesktop.Host` 設為啟始專案
5. 需要啟動 Agent 時，將 `RemoteDesktop.Agent` 設為啟始專案

## 執行需求

- Windows 10/11
- `.NET 8 SDK`
- `Visual Studio 2022`，建議安裝：
  - `.NET 桌面開發`
  - `ASP.NET 與 Web 開發`
- `SQL Server LocalDB` 或 `Microsoft SQL Server`

## 設定檔

### Host

檔案：`src/RemoteDesktop.Host/appsettings.json`

- `ConnectionStrings:RemoteDesktopDb`
  - MSSQL 連線字串。
- `ControlServer:ServerUrl`
  - Host 背景 Kestrel 監聽位址，預設為 `http://localhost:5106`
- `ControlServer:ConsoleName`
  - 主控台名稱。
- `ControlServer:AdminUserName`
  - WinForms 主控台登入帳號。
- `ControlServer:AdminPassword`
  - WinForms 主控台登入密碼。
- `ControlServer:SharedAccessKey`
  - Agent 與 Host 之間的共享金鑰。
- `ControlServer:AgentHeartbeatTimeoutSeconds`
  - Agent 心跳逾時秒數。

### Agent

檔案：`src/RemoteDesktop.Agent/appsettings.json`

- `Agent:ServerUrl`
  - Host 的 HTTP URL，Agent 會自動轉成 `ws://` 或 `wss://` 連線 `/ws/agent`
- `Agent:DeviceId`
  - 裝置識別碼。
- `Agent:DeviceName`
  - 裝置名稱。
- `Agent:SharedAccessKey`
  - 必須與 Host 相同。
- `Agent:CaptureFramesPerSecond`
  - 畫面傳輸 FPS。
- `Agent:JpegQuality`
  - JPEG 品質。
- `Agent:MaxFrameWidth`
  - 傳輸前縮圖最大寬度。
- `Agent:ReconnectDelaySeconds`
  - 斷線後重連等待秒數。

## 建置與執行

### CLI 建置

```powershell
$env:DOTNET_CLI_HOME="$PWD\\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
& 'C:\Program Files\dotnet\dotnet.exe' build .\RemoteDesktopSystem.csproj
```

### 啟動 Host

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Host\RemoteDesktop.Host.csproj
```

### 啟動 Agent

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj
```

## 目前行為

- 啟動 Host 後，會先顯示 WinForms 登入畫面。
- 登入成功後，主控台會背景啟動 Kestrel 並顯示：
  - 裝置清單
  - 在線數量
  - Presence Log
  - Agent 端點與健康檢查位址
- 主控台可直接雙擊在線裝置，開啟遠端畫面視窗。
- 遠端畫面視窗可傳送：
  - 滑鼠移動
  - 滑鼠按下/放開
  - 滾輪
  - 一般文字輸入
  - 常用控制鍵與方向鍵
- Agent 會顯示目前狀態與最近事件，方便在桌面端確認是否已成功連到 Host。

## 已知限制

- 目前 Host 的 viewer 改為單一檢視者模式，同一台裝置同時只允許一個遠端視窗。
- 目前登入驗證仍使用 `appsettings.json` 中的固定帳密，尚未改成安全儲存或雜湊驗證。
- 目前 Agent 仍以前景應用程式方式執行，尚未包成正式 Windows Service。
- Razor Pages 檔案仍保留在專案內，但不再參與目前 WinForms 主控台流程。

## 資料庫

- Host 啟動時會自動執行：
  - `src/RemoteDesktop.Host/DatabaseScripts/001_create_remote_desktop_schema.sql`
- 目前資料表：
  - `dbo.RemoteDesktopDevices`
  - `dbo.RemoteDesktopAgentPresenceLogs`

## 驗證結果

- 已完成 `dotnet build .\RemoteDesktopSystem.csproj`
- 建置結果：成功

