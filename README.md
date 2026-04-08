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
- `deploy/publish/Host`
  - Host 的 `win-x64 self-contained` 發佈版。
- `deploy/publish/Agent`
  - Agent 的 `win-x64 self-contained` 發佈版。
- `deploy/scripts`
  - 啟動腳本。
- `tests/RemoteDesktop.SmokeTests`
  - 核心通訊 smoke test。
- `tests/RemoteDesktop.UiAutomation`
  - WinForms UI 自動化測試。
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
- 補上 `publish` 發佈版、桌面捷徑與 Agent 開機自動啟動捷徑。

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

## 直接使用

### Publish 版

- Host：`deploy/publish/Host/RemoteDesktop.Host.exe`
- Agent：`deploy/publish/Agent/RemoteDesktop.Agent.exe`

### 桌面捷徑

已建立：
- `C:\Users\TECHUP\Desktop\RemoteDesktop Host.lnk`
- `C:\Users\TECHUP\Desktop\RemoteDesktop Agent.lnk`
- `C:\Users\TECHUP\Desktop\RemoteDesktop Host 設定資料夾.lnk`
- `C:\Users\TECHUP\Desktop\RemoteDesktop Agent 設定資料夾.lnk`

### Agent 開機啟動

已建立：
- `C:\Users\TECHUP\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\RemoteDesktop Agent 開機啟動.lnk`

## 建置與驗證

### Build

```powershell
$env:DOTNET_CLI_HOME="$PWD\\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
& 'C:\Program Files\dotnet\dotnet.exe' build .\RemoteDesktopSystem.sln
```

### Smoke Test

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.SmokeTests\RemoteDesktop.SmokeTests.csproj
```

### UI Automation

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.UiAutomation\RemoteDesktop.UiAutomation.csproj
```

## 文件

- 完整安裝與操作手冊：[INSTALLATION_GUIDE.md](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\INSTALLATION_GUIDE.md)
- 變更紀錄：[CHANGELOG.md](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\CHANGELOG.md)
- 待辦：[TODO.md](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\TODO.md)
