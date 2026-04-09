# RemoteDesktopSystem 安裝與操作手冊

## 1. 目的

本手冊提供目前 `RemoteDesktopSystem` 的正式安裝、設定、啟動、發佈與日常操作方式。

目前系統為：
- `RemoteDesktop.Host`：Windows Forms 主控台
- `RemoteDesktop.Agent`：Windows Forms 被控端 Agent
- 通訊方式：Host 背景 `Kestrel` + WebSocket `/ws/agent`
- 儲存模式：預設 `Memory`，可切換為 SQL Server / LocalDB

## 2. 系統架構

### 2.1 Host

`RemoteDesktop.Host` 提供：
- 管理者登入畫面
- 裝置清單
- 在線數量與連線紀錄
- 遠端畫面檢視與控制
- Remote Viewer `功能` 下拉選單
- Remote Viewer 全螢幕與縮放
- 剪貼簿同步
- 檔案上傳與下載
- Host 設定表單
- 背景健康檢查 `/healthz`

### 2.2 Agent

`RemoteDesktop.Agent` 提供：
- 桌面擷取
- 心跳回報
- 滑鼠與鍵盤輸入回放
- 自動重連
- `功能` 下拉選單
- Agent 設定表單

## 3. 需求

- Windows 10 / Windows 11
- 如使用原始碼執行：`.NET 8 SDK`
- 如使用 `publish` 版本：不需另裝 .NET
- 若要使用資料庫持久化：SQL Server LocalDB 或 SQL Server

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
- `ControlServer:PersistenceMode`
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
    "PersistenceMode": "Memory",
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
- Host 預設以 `Memory` 模式啟動，不會先連資料庫

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
5. 若要持久化裝置與歷程，可在設定中勾選「使用 MSSQL 儲存裝置與連線紀錄」
6. 雙擊在線裝置或按「開啟遠端畫面」即可開啟 Viewer

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

### 10.1 Agent 主畫面功能下拉

Agent 右上角提供 `功能 / Actions` 下拉，包含：

- `設定 / Settings`
- `複製裝置 ID / Copy device ID`
- `複製 Server 位址 / Copy server URL`
- `立即重新整理 / Refresh now`

適用情境：
- 現場快速複製 DeviceId 給 Host 管理員核對
- 快速確認 Agent 正在連哪一個 Host URL
- 設定更新後立即要求刷新狀態

## 11. Remote Viewer 操作方式

開啟方式：

1. 先啟動 Host 並登入
2. 確認 Agent 已在線
3. 在 Host 主畫面雙擊裝置，或按「開啟遠端畫面」
4. 開啟 `Remote Viewer`

Remote Viewer 右上角提供：

- `功能 / Actions` 下拉
- `縮放 / Zoom` 下拉

### 11.1 功能下拉

Host Remote Viewer 的 `功能 / Actions` 目前包含：

- `開啟資料夾 / Open Folder`
- `送出剪貼簿 / Send Clipboard`
- `取得剪貼簿 / Get Clipboard`
- `上傳檔案 / Upload File`
- `下載檔案 / Download File`
- `全螢幕 / Fullscreen`
- `聚焦遠端 / Focus Remote`
- `中斷連線 / Disconnect`

說明：
- `開啟資料夾`：開啟最近一次 upload/download 的本機或遠端對應資料夾；若尚未有傳輸紀錄，按鈕會停用。
- `送出剪貼簿`：將 Host 本機剪貼簿文字送到 Agent。
- `取得剪貼簿`：將 Agent 剪貼簿文字取回到 Host。
- `上傳檔案`：從 Host 選檔後傳到 Agent。
- `下載檔案`：輸入 Agent 端完整檔案路徑後，下載回 Host。
- `全螢幕`：切換全螢幕檢視。
- `聚焦遠端`：把鍵盤焦點切回遠端畫面。
- `中斷連線`：關閉目前 Viewer。

### 11.2 縮放與全螢幕

Remote Viewer 支援：

- `符合視窗`
- `50%`
- `75%`
- `100%`
- `125%`
- `150%`
- `200%`

操作規則：
- `符合視窗`：畫面會跟著視窗大小自動縮放。
- 手動縮放：畫面改為可捲動檢視。
- 遠端解析度未變時，手動縮放與捲動位置會保留，不會因串流刷新跳回原點。
- `F11`：切換全螢幕。
- `Esc`：離開全螢幕。
- 雙擊遠端畫面：切換全螢幕。

### 11.3 剪貼簿同步

支援情境：

- Host 將本機文字送到 Agent
- Host 取回 Agent 端文字剪貼簿

限制：
- 目前以文字剪貼簿為主
- 若使用者角色為僅觀看，剪貼簿功能會停用

### 11.4 檔案上傳

操作步驟：

1. 在 Viewer 右上角開啟 `功能 / Actions`
2. 按 `上傳檔案 / Upload File`
3. 在 Host 本機選擇要傳送的檔案
4. 等待 Viewer 顯示上傳進度
5. 完成後可使用 `開啟資料夾 / Open Folder`

目前行為：
- 傳輸區塊預設折疊，不會一直佔畫面
- 真的開始 upload/download 時才展開
- 完成後會顯示實際儲存路徑
- Host 與 Agent 皆會寫入檔案傳輸診斷日誌

Host 日誌位置：
- `deploy/publish/Host/logs/host-file-transfer.ndjson`

Agent 日誌位置：
- `deploy/publish/Agent/logs/agent-file-transfer.ndjson`

### 11.5 檔案下載

操作步驟：

1. 在 Viewer 右上角開啟 `功能 / Actions`
2. 按 `下載檔案 / Download File`
3. 輸入 Agent 端完整檔案路徑
4. 在 Host 選擇本機儲存位置
5. 等待傳輸完成
6. 完成後可用 `開啟資料夾 / Open Folder` 打開下載位置

目前限制：
- 這一版先採手動輸入 Agent 路徑
- 尚未提供遠端檔案總管瀏覽 UI

## 12. 連線驗證

### 12.1 手動驗證

- Host 啟動後，可在主畫面看到 Agent 上線
- Agent 啟動後，狀態應顯示已連線
- Host 雙擊在線裝置可開啟遠端畫面
- Viewer 應可看到 `功能 / Actions` 下拉
- Viewer 應可切換 `縮放 / Zoom`
- `F11` 與 `Esc` 應可切換全螢幕
- `上傳檔案 / Upload File` 與 `下載檔案 / Download File` 應能完成傳輸
- Agent 主畫面右上角應可看到 `功能 / Actions` 下拉

### 12.2 健康檢查

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5106/healthz
```

回傳內容會包含目前 `persistenceMode`。

### 12.3 自動化驗證

核心連線 smoke test：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.SmokeTests\RemoteDesktop.SmokeTests.csproj
```

WinForms UI automation：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.UiAutomation\RemoteDesktop.UiAutomation.csproj
```

## 13. 常見問題

### 13.1 Host 啟動顯示找不到 `ConnectionStrings:RemoteDesktopDb`

原因：
- 執行目錄沒有 `appsettings.json`
- 或目前啟用了 `SqlServer` 模式但連線字串不可用

目前已修正：
- `publish` 與 `build` 輸出都會自動帶上 `appsettings.json`
- 預設改為 `Memory` 模式，不會因為沒有 LocalDB 而阻止 Host 啟動

### 13.2 Agent 無法連上 Host

請檢查：
- Host 是否已啟動
- `Agent:ServerUrl` 是否正確
- `SharedAccessKey` 是否與 Host 完全一致
- 防火牆是否允許對應埠

### 13.3 資料庫初始化失敗

請檢查：
- `ControlServer:PersistenceMode` 是否真的需要設為 `SqlServer`
- `ConnectionStrings:RemoteDesktopDb` 是否可連線
- SQL Server/LocalDB 是否存在
- 帳號權限是否足夠

### 13.4 Remote Viewer 看得到畫面但無法控制某些程式

請檢查：

- Agent 是否允許 UAC 提權
- 目標程式是否為高權限視窗
- 是否為 Windows 安全桌面或受保護輸入畫面

目前系統已調整：
- Agent 使用較穩定的 `SendInput`
- 發佈版帶 `highestAvailable` manifest

### 13.5 檔案傳輸異常時要看哪裡

請優先檢查：

- `deploy/publish/Host/logs/host-file-transfer.ndjson`
- `deploy/publish/Agent/logs/agent-file-transfer.ndjson`

這兩份日誌會記錄：

- `start`
- `chunk`
- `complete`
- `abort`
- 錯誤訊息
- 實際落地路徑

## 14. 建議交付方式

如果要交給操作人員，建議直接提供：
- `deploy/publish/Host`
- `deploy/publish/Agent`
- 本手冊 `INSTALLATION_GUIDE.md`
- 桌面捷徑

## 15. 已驗證結果

- `dotnet build .\RemoteDesktopSystem.sln`：成功
- `tests/RemoteDesktop.SmokeTests`：成功
- `tests/RemoteDesktop.UiAutomation`：成功
- Host/Agent `appsettings.json` 已確認會輸出到執行目錄
- Agent 主畫面 `功能 / Actions` 下拉：已驗證
- Host Remote Viewer `功能 / Actions` 下拉：已驗證
- Viewer 縮放與全螢幕：已驗證
- Viewer upload/download 流程：已驗證
