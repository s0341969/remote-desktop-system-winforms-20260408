# RemoteDesktopSystem 操作設定安裝手冊

## 1. 文件目的

本手冊說明 `RemoteDesktopSystem` 的安裝、設定、啟動、驗證與日常操作方式，內容以目前專案內實際程式碼為準。

適用範圍：

- 控制端：`RemoteDesktop.Host`
- 被控端：`RemoteDesktop.Agent`
- 作業系統：`Windows 10 / Windows 11`
- 執行環境：`.NET 8`
- 資料庫：`MSSQL` 或 `SQL Server LocalDB`

## 2. 系統架構

系統由兩個主要元件組成：

1. `Control Server`
   - ASP.NET Core Razor Pages 控制台
   - 提供管理者登入、裝置清單、遠端檢視、上下線紀錄
   - 接收 Agent 上傳的桌面畫面
   - 轉送 Viewer 的輸入控制命令
2. `Windows Agent`
   - 執行於被控 Windows 主機
   - 擷取桌面畫面並送往 Host
   - 接收來自 Viewer 的滑鼠與鍵盤命令

## 3. 安裝前需求

### 3.1 軟體需求

- Windows 10 或 Windows 11
- `.NET 8 SDK`
- `SQL Server Express / Developer / Standard` 或 `SQL Server LocalDB`

### 3.2 網路需求

- 單機測試可直接使用 `localhost`
- 多機部署時：
  - Agent 必須可連到 Host 所在主機與對應埠
  - 若使用 HTTPS，Agent `ServerUrl` 必須填入 HTTPS URL
  - Windows 防火牆需允許 Host 對外連線

### 3.3 權限需求

- 執行 Agent 的 Windows 帳號必須可擷取桌面
- 執行 Agent 的 Windows 帳號必須可送出滑鼠與鍵盤輸入事件
- 若使用 SQL Server，請先準備具備資料庫權限的帳號

## 4. 專案目錄

- `RemoteDesktopSystem.csproj`
  - 聚合建置入口，會同時建置 Host 與 Agent
- `src/RemoteDesktop.Host`
  - 控制台網站
- `src/RemoteDesktop.Agent`
  - 被控端 Agent
- `src/RemoteDesktop.Host/DatabaseScripts/001_create_remote_desktop_schema.sql`
  - Host 啟動時自動執行的資料表初始化腳本

## 5. 安裝 .NET 8 SDK

請先安裝 `.NET 8 SDK`，再用以下指令確認：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --info
```

如果輸出中能看到 `.NET SDK 8.x`，表示安裝完成。

## 6. 準備資料庫

### 6.1 開發或單機測試

可直接使用專案預設的 LocalDB 連線字串：

```json
"ConnectionStrings": {
  "RemoteDesktopDb": "Server=(localdb)\\MSSQLLocalDB;Database=RemoteDesktopControl;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;"
}
```

### 6.2 多機或長期環境

建議改用實際 SQL Server，例如：

```json
"ConnectionStrings": {
  "RemoteDesktopDb": "Server=192.168.1.20;Database=RemoteDesktopControl;User ID=remote_desk_app;Password=StrongPassword!2026;Encrypt=True;TrustServerCertificate=True;"
}
```

### 6.3 資料表建立方式

Host 啟動時會自動執行：

- `src/RemoteDesktop.Host/DatabaseScripts/001_create_remote_desktop_schema.sql`

該腳本會建立：

- `dbo.RemoteDesktopDevices`
- `dbo.RemoteDesktopAgentPresenceLogs`

並建立索引、修正伺服器重啟後的裝置離線狀態。

## 7. 設定 Host

請編輯 `src/RemoteDesktop.Host/appsettings.json`。

建議最少修改為以下內容：

```json
{
  "ConnectionStrings": {
    "RemoteDesktopDb": "Server=(localdb)\\MSSQLLocalDB;Database=RemoteDesktopControl;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;"
  },
  "ControlServer": {
    "ConsoleName": "RemoteDesk Control",
    "AdminUserName": "admin",
    "AdminPassword": "ChangeThisStrongPassword!2026",
    "RequireHttpsRedirect": false,
    "SharedAccessKey": "ChangeThisAgentSharedKey!2026",
    "AgentHeartbeatTimeoutSeconds": 45
  }
}
```

### 7.1 欄位說明

- `ConsoleName`
  - 控制台標題
- `AdminUserName`
  - 管理者登入帳號，最少 3 個字元
- `AdminPassword`
  - 管理者登入密碼，最少 10 個字元
- `RequireHttpsRedirect`
  - 設為 `true` 時啟用 HTTPS 重新導向與 HSTS
- `SharedAccessKey`
  - Agent 驗證金鑰，最少 12 個字元，必須與 Agent 相同
- `AgentHeartbeatTimeoutSeconds`
  - 心跳逾時秒數，允許範圍 `15-300`

### 7.2 正式環境建議

- 必須更換預設管理者帳密
- 必須更換預設 `SharedAccessKey`
- 正式環境建議將 `RequireHttpsRedirect` 設為 `true`
- 建議搭配反向代理與 TLS 憑證

## 8. 設定 Agent

請編輯 `src/RemoteDesktop.Agent/appsettings.json`。

建議設定如下：

```json
{
  "Agent": {
    "ServerUrl": "http://127.0.0.1:5106",
    "DeviceId": "pc-accounting-01",
    "DeviceName": "會計室主機 01",
    "SharedAccessKey": "ChangeThisAgentSharedKey!2026",
    "CaptureFramesPerSecond": 8,
    "JpegQuality": 55,
    "MaxFrameWidth": 1600,
    "ReconnectDelaySeconds": 5
  }
}
```

### 8.1 欄位說明

- `ServerUrl`
  - Host 對外服務 URL
- `DeviceId`
  - 裝置唯一識別值，建議不可重複
- `DeviceName`
  - 控制台顯示名稱
- `SharedAccessKey`
  - 必須與 Host 完全一致
- `CaptureFramesPerSecond`
  - 畫面更新率，範圍 `1-24`
- `JpegQuality`
  - JPEG 品質，範圍 `30-90`
- `MaxFrameWidth`
  - 傳輸畫面最大寬度，範圍 `640-3840`
- `ReconnectDelaySeconds`
  - 斷線後重連秒數，範圍 `1-60`

### 8.2 建議值

- 一般內網：`CaptureFramesPerSecond = 8`
- 頻寬較小：可降為 `5`
- 畫質要求較高：可提高 `JpegQuality` 至 `65-75`
- 頻寬較敏感：可將 `MaxFrameWidth` 降為 `1280`

## 9. 建置系統

在專案根目錄執行：

```powershell
Set-Location 'C:\codex_pg\系統內建遠端桌面'
$env:DOTNET_CLI_HOME="$PWD\\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
& 'C:\Program Files\dotnet\dotnet.exe' build
```

此指令會同時建置：

- `src/RemoteDesktop.Host/RemoteDesktop.Host.csproj`
- `src/RemoteDesktop.Agent/RemoteDesktop.Agent.csproj`

## 10. 啟動 Host

在專案根目錄執行：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Host\RemoteDesktop.Host.csproj
```

開發模式下預設可使用：

- `http://localhost:5106`
- `https://localhost:7242`

### 10.1 首次啟動時會執行

- 讀取 `appsettings.json`
- 驗證 `ControlServer` 設定欄位
- 連線到 MSSQL
- 自動執行資料表初始化腳本
- 啟用 Razor Pages、Cookie 驗證、WebSocket 與健康檢查端點

### 10.2 健康檢查

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5106/healthz
```

預期回傳類似：

```json
{
  "status": "ok",
  "onlineDevices": 0,
  "totalDevices": 0
}
```

## 11. 啟動 Agent

在被控端主機執行：

```powershell
Set-Location 'C:\codex_pg\系統內建遠端桌面'
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj
```

### 11.1 Agent 啟動流程

1. 讀取 `Agent` 設定
2. 連線到 `/ws/agent`
3. 送出裝置資訊與 `SharedAccessKey`
4. 送出初始螢幕尺寸
5. 持續送出心跳與 JPEG 畫面資料
6. 接收 Viewer 的滑鼠與鍵盤控制命令

### 11.2 成功判斷方式

- Host 首頁可看到該裝置
- 裝置狀態顯示為在線
- `RemoteDesktopDevices` 有新增或更新資料
- `RemoteDesktopAgentPresenceLogs` 會新增一筆連線紀錄

## 12. 控制台登入與操作

### 12.1 登入

1. 開啟瀏覽器
2. 進入 Host URL，例如 `http://localhost:5106`
3. 輸入：
   - `AdminUserName`
   - `AdminPassword`
4. 成功後進入首頁

### 12.2 首頁可操作內容

- 查看在線裝置數量
- 查看總裝置數量
- 查看每台裝置的：
  - `DeviceName`
  - `DeviceId`
  - `HostName`
  - 解析度
  - 最後心跳時間
  - Agent 版本
- 進入連線記錄頁
- 開啟遠端檢視頁面

### 12.3 開啟遠端檢視

1. 在首頁選擇在線裝置
2. 進入 Viewer 頁面
3. 等待畫面串流載入
4. 將焦點切到遠端畫面後開始送出操作命令

## 13. 驗證清單

部署完成後，至少驗證以下項目：

- `dotnet build` 成功
- Host 可正常啟動
- `/healthz` 回傳 `status = ok`
- Agent 可成功連線
- Host 首頁可看到裝置
- Viewer 可正常顯示畫面
- Agent 關閉後裝置會轉為離線
- `RemoteDesktopAgentPresenceLogs` 有連線與斷線紀錄

## 14. 常見問題排除

### 14.1 Agent 無法連線到 Host

請檢查：

- `Agent:ServerUrl` 是否正確
- Host 是否已啟動
- 防火牆是否開放
- `SharedAccessKey` 是否與 Host 一致
- 若使用 HTTPS，URL 與憑證是否正確

### 14.2 Host 啟動時資料庫錯誤

請檢查：

- `ConnectionStrings:RemoteDesktopDb` 是否正確
- SQL Server / LocalDB 是否存在
- 帳號是否有資料庫權限
- SQL Server 是否允許目前的驗證方式

### 14.3 裝置在線但沒有畫面

請檢查：

- Agent 是否仍在執行
- Agent 是否有畫面擷取權限
- 目標主機是否處於無法互動的特殊桌面狀態
- 網路是否穩定

### 14.4 Viewer 開不起來

請檢查：

- 管理者是否已登入
- 該裝置是否在線
- 瀏覽器是否允許 WebSocket
- 反向代理是否正確處理 WebSocket 升級

## 15. 安全注意事項

- 預設帳號密碼僅適用開發測試，部署前必須更換
- `SharedAccessKey` 不可使用預設值
- 正式環境建議使用 HTTPS
- 正式環境建議將密碼與金鑰移出版本控制設定檔
- 若部署於跨網段或公開網路，應加上反向代理、TLS、IP 限制與更完整的身份驗證

## 16. 已知限制

- 目前為單一管理者帳密模式
- Agent 尚未封裝為 Windows Service
- 尚未提供安裝程式
- 尚未整合 AD / LDAP / SSO
- 專案內部分 UI 檔案仍有舊編碼遺留問題
