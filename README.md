# RemoteDesktopSystem

`RemoteDesktopSystem` 現在是純 Windows Forms 控制端與 Agent 的遠端桌面系統，保留既有 WebSocket 通訊核心與 MSSQL 裝置紀錄，並移除已停用的 Razor Pages 舊 Web UI。

## 目前架構

- `RemoteDesktopSystem.sln`
  - 給 `Visual Studio 2022` 直接開啟的解決方案。
- `src/RemoteDesktop.Host`
  - Windows Forms 主控台。
  - 背景自架 `Kestrel`，提供 `/ws/agent` 與 `/healthz`。
  - 前景提供登入、儀表板、遠端檢視與 Host 設定表單。
- `src/RemoteDesktop.Agent`
  - Windows Forms Agent。
  - 提供桌面截圖、心跳、輸入回放、自動重連與 Agent 設定表單。
- `tests/RemoteDesktop.SmokeTests`
  - 核心通訊 smoke test。
  - 驗證 Agent 註冊、Viewer 收圖、Viewer 指令轉送。
- `tests/RemoteDesktop.UiAutomation`
  - WinForms UI 自動化測試。
  - 驗證登入、Host 設定、Agent 設定、Host 主畫面與 Agent 主畫面。
- `RemoteDesktopSystem.csproj`
  - 根目錄聚合建置檔，用來一次建置主要執行專案。

## 本次整理重點

- 補齊 Host 與 Agent 的完整設定表單，改由 UI 編輯 `appsettings.json`。
- 新增 `HostSettingsStore` 與 `AgentSettingsStore`，集中設定檔讀寫與驗證。
- Host 主畫面新增設定入口，Agent 主畫面新增設定入口。
- 移除 `src/RemoteDesktop.Host/Pages` 與 `src/RemoteDesktop.Host/wwwroot` 舊碼，Host 專案不再保留停用的 Razor Pages。
- 新增 `tests/RemoteDesktop.UiAutomation`，把主要 WinForms 使用流程納入自動化驗證。
- 將 UI automation 專案加入 `RemoteDesktopSystem.sln`。
- 保留既有 smoke test，持續驗證核心 WebSocket 與 broker 流程。

## 使用 Visual Studio 2022

1. 開啟 `RemoteDesktopSystem.sln`
2. 在方案總管中選擇要編修的專案或表單
3. 目前可由設計器直接編修的主要表單：
   - `src/RemoteDesktop.Host/Forms/LoginForm.cs`
   - `src/RemoteDesktop.Host/Forms/MainForm.cs`
   - `src/RemoteDesktop.Host/Forms/RemoteViewerForm.cs`
   - `src/RemoteDesktop.Host/Forms/Settings/HostSettingsForm.cs`
   - `src/RemoteDesktop.Agent/Forms/AgentMainForm.cs`
   - `src/RemoteDesktop.Agent/Forms/Settings/AgentSettingsForm.cs`
4. 需要啟動控制端時，將 `RemoteDesktop.Host` 設為啟始專案
5. 需要啟動 Agent 時，將 `RemoteDesktop.Agent` 設為啟始專案
6. 需要驗證核心通訊時，執行 `RemoteDesktop.SmokeTests`
7. 需要驗證 WinForms UI 時，執行 `RemoteDesktop.UiAutomation`

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
  - Host 背景 Kestrel 監聽位址，預設 `http://localhost:5106`
- `ControlServer:ConsoleName`
  - 主控台名稱。
- `ControlServer:AdminUserName`
  - WinForms 主控台登入帳號。
- `ControlServer:AdminPassword`
  - WinForms 主控台登入密碼。
- `ControlServer:SharedAccessKey`
  - Agent 與 Host 間共享金鑰。
- `ControlServer:AgentHeartbeatTimeoutSeconds`
  - Agent 心跳逾時秒數。
- `ControlServer:RequireHttpsRedirect`
  - 是否啟用 HTTPS 轉址。

### Agent

檔案：`src/RemoteDesktop.Agent/appsettings.json`

- `Agent:ServerUrl`
  - Host 的 HTTP URL，預設 `http://localhost:5106`
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
& 'C:\Program Files\dotnet\dotnet.exe' build .\RemoteDesktopSystem.sln
```

### 啟動 Host

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Host\RemoteDesktop.Host.csproj
```

### 啟動 Agent

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj
```

### 執行 Smoke Test

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.SmokeTests\RemoteDesktop.SmokeTests.csproj
```

### 執行 WinForms UI 自動化測試

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.UiAutomation\RemoteDesktop.UiAutomation.csproj
```

## 目前行為

- Host 啟動後，先顯示 WinForms 登入畫面。
- 登入成功後，主控台背景啟動 Kestrel，並顯示：
  - 裝置清單
  - 在線數量
  - Presence Log
  - Agent 端點與健康檢查位址
- Host 與 Agent 的 `appsettings.json` 會隨建置結果自動複製到輸出目錄，直接執行 `bin\Debug\net8.0-windows\*.exe` 也能讀到設定。
- Host 主畫面可直接開啟設定表單，修改連線字串、主控台參數、帳號密碼與共享金鑰。
- Agent 主畫面可直接開啟設定表單，修改控制端 URL、裝置資訊、影像參數與重連策略。
- 設定寫入 `appsettings.json` 後，重新啟動對應應用程式即可生效。
- 主控台可直接雙擊在線裝置開啟遠端畫面視窗。
- 遠端畫面視窗可傳送滑鼠、滾輪、文字輸入與常用控制鍵。

## 已知限制

- 目前 Host 的 viewer 為單一檢視者模式，同一台裝置同時只允許一個遠端視窗。
- 目前登入驗證仍使用 `appsettings.json` 中的固定帳密，尚未改成安全儲存或雜湊驗證。
- 目前 Agent 仍以前景應用程式方式執行，尚未包成正式 Windows Service。
- WinForms UI 自動化測試目前驗證表單互動與資料提交流程，尚未覆蓋實際遠端檢視視窗的畫面/輸入回放自動化。

## 資料庫

- Host 啟動時會自動執行：
  - `src/RemoteDesktop.Host/DatabaseScripts/001_create_remote_desktop_schema.sql`
- 目前資料表：
  - `dbo.RemoteDesktopDevices`
  - `dbo.RemoteDesktopAgentPresenceLogs`

## 驗證結果

- 已完成 `dotnet build .\RemoteDesktopSystem.sln`
- 建置結果：成功
- 已完成 `dotnet run --project .\tests\RemoteDesktop.SmokeTests\RemoteDesktop.SmokeTests.csproj`
- smoke test 結果：成功
- 已完成 `dotnet run --project .\tests\RemoteDesktop.UiAutomation\RemoteDesktop.UiAutomation.csproj`
- UI automation 結果：成功
