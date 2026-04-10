# RemoteDesktopSystem

`RemoteDesktopSystem` 現在是純 Windows Forms 控制端與 Agent 的遠端桌面系統，保留既有 WebSocket 通訊核心，並提供可切換的記憶體模式或 MSSQL 裝置紀錄儲存。

## 目前架構

- `RemoteDesktopSystem.sln`
  - 給 `Visual Studio 2022` 直接開啟的解決方案。
- `src/RemoteDesktop.Host`
  - Windows Forms 主控台。
  - 背景自架 `Kestrel`，提供 `/ws/agent` 與 `/healthz`。
  - 前景提供登入、儀表板、遠端檢視與 Host 設定表單。
- `src/RemoteDesktop.Server`
  - 中央 Host Server。
  - 可獨立啟動，提供 `/ws/agent`、`/ws/viewer`、`/ws/dashboard`、`/healthz` 與中央管理 API。
- `src/RemoteDesktop.Shared`
  - 第一階段新增的共享契約專案。
  - 集中放 Agent/Viewer 通訊模型與裝置資料 DTO，供後續 Server / Console Client 共用。
- `src/RemoteDesktop.Agent`
  - Windows Forms Agent。
  - 提供桌面截圖、心跳、輸入回放、自動重連與 Agent 設定表單。
- `deploy/publish/Host`
  - Host 的精簡 `win-x64 framework-dependent` 發佈版。
- `deploy/publish/Agent`
  - Agent 的精簡 `win-x64 framework-dependent` 發佈版。
- `deploy/publish/Server`
  - Server 的精簡 `framework-dependent` 發佈版。
- `deploy/scripts`
  - 發佈、清理與整包交付腳本。
- `tests/RemoteDesktop.SmokeTests`
  - 核心通訊 smoke test。
- `tests/RemoteDesktop.UiAutomation`
  - WinForms UI 自動化測試。
- `RemoteDesktopSystem.csproj`
  - 根目錄聚合建置檔，用來一次建置主要執行專案。

## 本次整理重點

- 補齊 Host 與 Agent 的完整設定表單，改由 UI 編輯 `appsettings.json`。
- 新增 `HostSettingsStore` 與 `AgentSettingsStore`，集中設定檔讀寫與驗證。
- Host 預設改為 `Memory` 儲存模式，不再要求先安裝 LocalDB 才能啟動；需要持久化時可在 Host 設定中勾選資料庫模式。
- Host 主畫面新增設定入口，Agent 主畫面新增設定入口。
- 移除 Agent 不需要的 ASP.NET Framework 參考，精簡發佈目錄，清掉多餘的 `Microsoft.AspNetCore.*` DLL。
- 移除 `src/RemoteDesktop.Host/Pages` 與 `src/RemoteDesktop.Host/wwwroot` 舊碼，Host 專案不再保留停用的 Razor Pages。
- 新增 `tests/RemoteDesktop.UiAutomation`，把主要 WinForms 使用流程納入自動化驗證。
- 將 UI automation 專案加入 `RemoteDesktopSystem.sln`。
- 保留既有 smoke test，持續驗證核心 WebSocket 與 broker 流程。
- 補上 `publish` 發佈版、桌面捷徑與 Agent 開機自動啟動捷徑。
- 新增 `deploy/scripts/Clean-App.ps1`，可一鍵清理 `bin/obj`、`.dotnet` 與執行期垃圾檔。
- `deploy/scripts/Publish-App.ps1` 現在會先清空輸出目錄，再以最小必要相依與 `zh-Hant` 資源重建 publish 版，避免舊的 self-contained DLL 殘留。
- 修正檔案上傳流程造成 Viewer 卡頓的問題：Host 上傳改為背景傳輸，Agent 端進度訊息改為節流回報，並將單一 chunk 降到 16 KB，避免大檔案傳輸時產生過大的 Base64 暫存字串。
- Host 的上傳按鈕事件現在有最外層例外保護；若實際環境仍遇到傳輸異常，會顯示錯誤訊息而不是直接讓整個 Viewer 當掉。
- 上傳按鈕事件改為非阻塞背景工作，避免 WinForms 事件本身長時間佔住 UI 訊息迴圈。
- Viewer 在上傳完成後會直接顯示 Agent 端實際儲存位置，並提供「開啟資料夾」按鈕快速打開目的資料夾。
- Upload 路徑的權限不足情境改為非阻塞式狀態更新與診斷日誌，不再透過可能被遮住的 modal 對話框卡住整個 Viewer。
- Upload 按鈕現在會先回到 UI 訊息迴圈再開啟選檔流程，並額外寫入同步 fallback marker，避免 click 事件與 modal 對話框重入時整個 Viewer 無回應。
- Host 的檔案選擇器現在改由獨立 STA 執行緒開啟，不再依賴 Viewer 當前 UI 執行緒的 modal 狀態，避免選檔視窗無法彈出導致上傳流程卡死。
- Viewer 的傳輸區塊改為分開顯示「狀態」與「目的地路徑」，並重新安排進度列位置，避免長路徑把文字與進度列擠在一起。
- Viewer 新增縮放下拉選單，可在「符合視窗」與 50%-200% 間切換；手動縮放時會改為可捲動檢視，方便放大局部畫面。
- 縮放下拉選單的同步邏輯已避免在串流刷新期間反覆改寫 `SelectedIndex`，修正實機操作時下拉選單無法穩定挑選、看起來像一直跳出的問題。
- 手動縮放時，Viewer 現在不會在每一張串流畫面都重新排版；若遠端解析度沒有改變，會保留目前的手動縮放與捲動位置，不再一直跳回左上角。
- Viewer 新增全螢幕模式，可用按鈕或雙擊遠端畫面切換，並支援 `F11` 進入/退出與 `Esc` 離開全螢幕。
- Viewer 的傳輸區塊預設折疊，只有在實際 upload/download 開始後才展開並顯示狀態、路徑與進度列。
- Viewer 新增遠端檔案總管，可直接瀏覽 Agent 端資料夾、移動遠端項目，並從總管內選取檔案下載到 Host。
- Host Viewer 右上角的 Agent 操作已改成 `功能` 下拉按鍵，集中收納開啟資料夾、剪貼簿同步、upload/download、全螢幕、聚焦 Viewer 與中斷連線。
- Host 的登入窗、主控台、Viewer，以及 Agent 主畫面現在都會顯示 build 版本與 EXE 建置時間，方便直接確認目前執行中的是否為最新發佈版。
- Agent 主畫面的操作入口已改成右上角 `功能` 下拉按鍵，集中提供設定、複製裝置 ID、複製 Server 位址與立即重新整理。
- 第一階段新增 `RemoteDesktop.Server` 與 `RemoteDesktop.Shared`，把中央 Host Server 所需的通訊契約、裝置儲存與 Agent WebSocket 通道獨立出來，為後續多主控台 Console Client 做準備。
- 第二階段讓 `RemoteDesktop.Host` 可透過 `ControlServer:CentralServerUrl` 切換成中央 Server 儀表板模式；此模式下主畫面會改抓中央 Server 的裝置清單、在線紀錄與授權更新，Viewer、遠端畫面串流與 Viewer 指令轉送也已改由中央 Server websocket 中繼。
- 第七階段補上中央儀表板 WebSocket 推播 `/ws/dashboard`，中央模式的 Host 主畫面改為「事件推播 + 低頻輪詢回補」；裝置上線、離線與授權異動會即時刷新，多台主控台不再只靠固定 5 秒輪詢。
- 第八階段補上中央 Host 設定 API `/api/settings/host`，中央模式下的 Host 設定表單會改走 Server 儲存；只有 `CentralServerUrl` 仍保留在每台 Console Client 本機，作為該主控台要連哪一台中央 Server 的入口。
- 發佈流程已補齊中央 Server：`Publish-App.ps1` 現在支援依專案指定 framework，`Deploy-App.ps1` 可一鍵 clean、build、測試、publish Host/Agent/Server，並重建 `deploy/release/current`、日期版資料夾與 zip 交付包。
- 新增 `deploy/scripts/Start-Server.cmd` 與 `deploy/scripts/Publish-Server-Launcher.cmd`，讓中央 Server 也能走與 Host/Agent 一致的交付與啟動流程。
- `RemoteDesktop.Server` 已實測可獨立啟動、可回 `/healthz`，並能接受 Agent `hello-ack` / `heartbeat` 協定。
- Agent 現在使用較完整的 Win32 輸入注入路徑，鍵盤改用 scan code，滑鼠移動改用絕對座標 `SendInput`，並在未提權時於 Agent 狀態中主動提示高權限視窗可能拒絕接收輸入。
- Agent 發佈版現在帶有 `highestAvailable` manifest，讓系統可在有權限時直接提升，改善高權限應用程式無法操控的情況。
- Host 的本機剪貼簿讀寫改為專用 STA 路徑執行，不再因目前執行緒 apartment 狀態不同而直接失敗。
- Host 的檔案選擇對話框改回同步 UI 執行緒開啟，實際上傳仍維持背景工作執行，修正真機上傳時只留下 host-upload-clicked 而沒有開啟檔案選擇器的卡點。
- Host / Agent 現在都會將檔案傳輸流程寫入 `logs` 目錄，方便追查 `start/chunk/complete/abort` 卡在哪一段。
- Host 日誌：`deploy/publish/Host/logs/host-file-transfer.ndjson`
- Agent 日誌：`deploy/publish/Agent/logs/agent-file-transfer.ndjson`
- `tests/RemoteDesktop.UiAutomation` 現在已涵蓋 Viewer 檔案上傳、目的地顯示與開啟資料夾流程。
- `tests/RemoteDesktop.UiAutomation` 現在也涵蓋遠端檔案總管的載入、切換資料夾、移動與下載流程。

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
- Server：`deploy/publish/Server/RemoteDesktop.Server.exe`
- Host 預設 `ControlServer:PersistenceMode = Memory`，可直接啟動不連資料庫。
- Publish 目錄會在每次重建時完整覆蓋；若有自訂設定，應修改 `src/.../appsettings.json` 或在發佈後另外備份部署設定。
- 可交付壓縮包與固定部署資料夾會輸出到 `deploy/release`。
- 若要改回 MSSQL：
  1. 開啟 Host 的設定表單
  2. 勾選「使用 MSSQL 儲存裝置與連線紀錄」
  3. 填入有效的 `RemoteDesktopDb` 連線字串
  4. 重新啟動 Host

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

### 啟動中央 Server

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Server\RemoteDesktop.Server.csproj
```

預設：
- `ControlServer:ServerUrl = http://localhost:5206`
- `PersistenceMode = Memory`

目前定位：
- 這是中央 Host Server
- 現在 `RemoteDesktop.Host` 已可透過 `ControlServer:CentralServerUrl` 接這個 Server，主畫面裝置清單/在線紀錄/授權管理會改走中央 API
- Viewer attach/detach、遠端畫面串流與 Viewer 指令也會改走中央 Server 的 `/ws/viewer` 通道
- Host 登入、使用者管理、稽核與 Host 設定畫面在中央模式下也已改走 `RemoteDesktop.Server` API
- 中央模式現在已補真正的 bearer token/session；`/api/devices`、`/api/presence-logs`、`/api/users`、`/api/audit-logs`、`/ws/viewer` 與 `/ws/dashboard` 都改由 Server 端驗證登入 session 與角色，不再信任 Console Client 傳入的 `userName` / `canControl`。`/ws/viewer` 也已補上中央 Viewer Session Lock：同一台裝置同時間只會有一個控制者，其餘 Viewer 會自動降為僅觀看，並可由具控制權角色透過「取得控制權 / Take Control」強制接管。

### Clean

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Clean-App.ps1 -IncludeDotnetHome
```

若要連 `deploy/publish` 也一起清空：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Clean-App.ps1 -IncludeDotnetHome -IncludePublish
```

### Publish

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Publish-App.ps1 `
  -ProjectRelativePath 'src\RemoteDesktop.Host\RemoteDesktop.Host.csproj' `
  -OutputRelativePath 'deploy\publish\Host' `
  -ExecutableName 'RemoteDesktop.Host.exe' `
  -Framework 'net8.0-windows'

& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Publish-App.ps1 `
  -ProjectRelativePath 'src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj' `
  -OutputRelativePath 'deploy\publish\Agent' `
  -ExecutableName 'RemoteDesktop.Agent.exe' `
  -Framework 'net8.0-windows'

& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Publish-App.ps1 `
  -ProjectRelativePath 'src\RemoteDesktop.Server\RemoteDesktop.Server.csproj' `
  -OutputRelativePath 'deploy\publish\Server' `
  -ExecutableName 'RemoteDesktop.Server.exe' `
  -Framework 'net8.0'
```

### 一鍵部署

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Deploy-App.ps1
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


















