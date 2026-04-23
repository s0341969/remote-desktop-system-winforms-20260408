# Changelog

## 2026-04-20

- 修正 `RemoteDesktop.Agent.exe` 在 Windows 7 上啟動時跳出 `無法找到輸入點 LocateXStateFeature (KERNEL32.dll)` 的相容性問題；Agent 專案已改回 `net6.0-windows`，正式 publish 版也改成 `win7-x64 self-contained`，讓 Win7 仍可直接啟動，同時保留與現有 `net8` Host / Server 的通訊相容性。
- 再補 `RemoteDesktop.Agent` 的雙架構交付：現在會在同一個 `deploy/publish/Agent` 目錄同時輸出 `RemoteDesktop.Agent.x64.exe` 與 `RemoteDesktop.Agent.x86.exe`，分別對應 64 位元與 32 位元 Windows；`Start-Agent.cmd` 也會依目前作業系統位元數自動挑選正確執行檔。
- 依最新交付需求調整 Agent 雙架構檔名：`deploy/publish/Agent` 內的 64 位元版本固定為 `RemoteDesktop.Agent.exe`，32 位元維持 `RemoteDesktop.Agent.x86.exe`；`Start-Agent.cmd` 與 publish 腳本已同步更新。
- 修正 Host 主控台 GridView 複製欄位值時偶發的 `Current thread must be set to STA` 例外；現在 `已連線裝置` 與 `在線紀錄` 會改用自訂的 STA 剪貼簿寫入流程處理 `Ctrl + C`，只複製目前選中的單格內容。
- 調整 `RemoteDesktopAgentPresenceLogs` 寫入策略：同一台裝置只會在真正從離線轉為上線時新增一筆 presence log；若仍屬同一次在線期間內的重連或重新註冊，現在會沿用未關閉的既有紀錄並更新 `LastSeenAt`、IP、版本與主機資訊，不再反覆新增多筆「在線中」紀錄。
- 再調整 `RemoteDesktopAgentPresenceLogs` 合併規則：同一台裝置若離線原因 `DisconnectReason` 與上一筆已關閉紀錄相同，現在會更新既有紀錄而不是新增新筆數；只有離線原因改變時才保留新的在線歷史，降低 SQL 與主控台在線紀錄表的成長速度。
- 再調整角色權限：`Operator` 現在可開啟 Viewer 並控制遠端電腦，中央 Server 也會核發控制權；但 `Operator` 的上傳/下載檔案功能已關閉，只有 `Administrator` 可使用檔案傳輸。
- 中央 Server 的 `UserAccount` store 也已改支援 MSSQL；當 `PersistenceMode = SqlServer` 時，使用者帳號會改讀寫 `dbo.RemoteDesktopUserAccounts`，若資料庫目前沒有帳號，會先自動匯入既有 `users.json`，沒有 JSON 時則建立 bootstrap 管理員。
- 中央 Server 新增 MSSQL 型 `Host 設定` 與 `稽核紀錄` 儲存；當 `PersistenceMode = SqlServer` 時，`/api/settings/host` 會改讀寫 `dbo.RemoteDesktopServerSettings`，稽核內容會改寫 `dbo.RemoteDesktopAuditLogs`，不再只留在 `appsettings.json` 與 `audit-log.ndjson`。
- 調整角色權限：`Operator` 現在可控制遠端並使用一般 Viewer 控制操作，但仍不可上傳或下載檔案；`Viewer` 維持觀看模式；只有 `Administrator` 可完整使用 Viewer 與檔案傳輸。
- 調整 Host 主控台的時間顯示與時間寫入策略：`已連線裝置`、`在線紀錄` 現在會直接顯示 `yyyy-MM-dd HH:mm:ss zzz`，新的 `Memory` / `SqlServer` 紀錄改以主機本地 `DateTimeOffset.Now` 寫入，避免資料庫與畫面因 UTC 轉本地時間而看起來不一致。
- 修正 Host 主控台 header / summary 版面在長 build 字串、查詢列與長 URL 下的文字遮蓋問題；標題、查詢區與摘要欄位現在會依視窗寬度自動重新排版與省略。
- 再調整 Host 主控台的單台裝置查詢列位置，改為固定顯示在「已連線裝置」區塊標題旁，不再因主標題區重排而被擠掉或看起來消失。
- 將 Host 主控台的 `已連線裝置` 與 `在線紀錄` GridView 選取模式改為單格選取，並保留原本依目前裝置列判斷操作按鈕與刷新後還原選取位置的行為。
- 修正 Host 主控台在中央模式下可能因大量 dashboard push 事件而持續刷新、造成整頁閃爍的問題；現在會先做短時間事件節流，Grid 重綁時也會暫停重繪，降低畫面閃爍感。
- Host 主控台新增「查詢主機名稱 / IP」輸入框，可依裝置 ID、裝置名稱、主機名稱與 IP 位址即時篩選「已連線裝置」與「在線紀錄」。
- Host 主控台的「已連線裝置」與「在線紀錄」新增 IP 位址欄位，並保留既有的全欄位排序能力。
- Host / Server 現在會在 Agent 註冊時擷取遠端 IP，並將 IP 同步保存到 `Memory` repository、`SqlServer` 的 `RemoteDesktopDevices` 與 `RemoteDesktopAgentPresenceLogs`。
- Host / Server 的資料庫初始化腳本新增 `RemoteIpAddress` 欄位相容升級邏輯，既有資料庫啟動後可直接補欄位，不需要手動重建資料表。
- 修正 Host Viewer 的下載流程在實機上可能卡住、不再跳出下載目的地視窗的問題；下載按鈕現在會先 dispatch 回 UI 訊息迴圈，`SaveFileDialog` 也改回優先由 UI 執行緒顯示，避免跨執行緒 owner/等待造成整個 Viewer 停住。
- 修正 Agent 下載遠端檔案時的開檔分享模式；現在會用允許 `ReadWrite/Delete` 的共享讀取方式開啟檔案，改善遠端檔案仍被應用程式開啟時無法下載的問題，只有真正的獨占鎖才會繼續拒絕。
- 修正 Host 本機下載落地路徑：下載暫存檔改為每次使用唯一檔名，完成時若原本指定的本機目的檔被占用，會自動改存成 `檔名 (1)`、`檔名 (2)` 等可用名稱，避免同名檔或殘留暫存檔把下載誤判成失敗。
- 修正 Agent 盤點在部分電腦上抓不到作業系統版本與硬體摘要的問題；現在 WMI 失敗時會再改用 registry、`RuntimeInformation`、`Environment.OSVersion` 與 `GlobalMemoryStatusEx` 補抓 CPU、記憶體與 Windows 版本資訊。
- 修正 `SqlServer` 模式的 inventory 歷史判定：現在不再把 `CollectedAt` 視為內容變更，只有 CPU、記憶體、磁碟、OS、Office 或最後更新等實際盤點內容改變時才新增 `RemoteDesktopInventoryHistory` 紀錄。
- 修正「裝置詳細資訊」匯出 CSV / Excel 時可能卡住的問題；匯出目的地 `SaveFileDialog` 現在改用獨立 STA 執行緒顯示，實際寫檔也改成非阻塞背景工作，不再讓詳細資訊視窗在匯出期間假死。
- 修正 Host 主控台在 `CellSelect` 模式下可能出現「清單顯示在線，但開啟 Viewer 卻跳成離線」的問題；開啟 Viewer 前現在會先依目前儲存格列抓取裝置，再向資料來源重新確認一次最新狀態。
- 修正 Agent 在 Windows Server / 微軟遠端桌面 session 切換後可能因桌面擷取失敗而整條 WebSocket 反覆重連的問題；現在畫面擷取失敗只會記 warning 並持續重試，Agent 連線本身不會因此一下在線一下離線。
- 再修正 Windows Server / RDP session 切換後的 Viewer 黑畫面：Agent 現在不會再把幾乎全黑的 frame 當成正常畫面送出；Viewer 若持續收不到有效 frame，會直接顯示桌面目前不可擷取的狀態訊息。
- 再補 Windows Server 的 session 自動恢復：Agent 新增 `Agent:AutoRecoverInteractiveSessionOnWindowsServer`，預設會在 RDP 關閉後桌面不可擷取時嘗試執行 `tscon <目前 session> /dest:console`，將互動桌面切回 console，改善只有 mstsc 開著時 Viewer 才有畫面的情況。
- 新增 repo 級別的集中 build 版本規則；Host / Agent / Server 現在每次 build / publish 都會自動產生新的四段版號 `主版.次版.yyDDD.HHmm`，且 Agent `hello` 上報與主控台 `Agent 版本` 欄位改為直接使用同一個 build 版本來源，方便控管現場版本。
- 補強 Agent / Host 的版本字串讀取容錯；若單檔 publish EXE 的版本資源讀取異常，現在會自動退回組件版本，不再讓 `AgentVersion` 解析失敗影響 Agent 註冊或主控台顯示。
- 修正中央 `SqlServer` 模式下新版 Agent 可能顯示離線的回歸：關閉 SDK 對 `InformationalVersion` 自動附加 source revision，並在 Host / Server 的 schema 初始化時自動擴充 `RemoteDesktopDevices` 與 `RemoteDesktopAgentPresenceLogs` 的 `AgentVersion`、`DeviceName`、`HostName` 欄位長度，避免 SQL `字串或二進位資料會被截斷` 導致 Agent 註冊後立刻被 Server 關閉。

## 2026-04-19

- 修正 Host 主控台 `MainForm` 的裝置清單與在線紀錄 GridView 刷新行為：背景輪詢或 dashboard push 更新時，現在會保留目前排序、選取列與卷動位置，避免整個表格反覆跳動。
- Host 主控台的裝置清單與在線紀錄現在支援所有欄位點擊排序，並會在後續自動刷新時延續使用者目前選定的排序欄位與方向。
- 修正 Host / Server 的 Agent WebSocket 關閉容錯：當 Agent 重連取代舊連線，或對端直接斷線未完成 close handshake 時，現在會安全 close/abort 並持續完成 repository cleanup，不再把這類可預期斷線記成 `Agent WebSocket processing failed`。
- 再修正 Host / Server 的 Agent socket 關閉判定：若 `CloseAsync` 期間底層 socket 已先被 dispose/abort，現在會視為預期結束，不再額外記錄 `Closing agent socket timed out ... ObjectDisposedException` warning。

## 2026-04-11

- Host Remote Viewer 新增「切換登入畫面」動作，會透過 Agent 要求遠端 Windows 切到登入/鎖定畫面；因 Windows 限制，一般桌面程式不能直接模擬標準 `Ctrl + Alt + Del`，因此改用 `LockWorkStation()` 提供穩定可用的等效行為。
- 修正 Host 檔案傳輸相關對話框的前景行為：上傳、下載、遠端檔案總管與目的資料夾挑選器現在會以 Viewer 為 owner 並主動帶到最前面，避免切換槽區或選檔時被 Remote Viewer 介面遮住。
- 遠端檔案總管與移動目的地資料夾挑選器新增「槽區 / Drive」下拉，會顯示 Agent 端可瀏覽的磁碟根目錄，支援直接切換 `C:`、`D:`、`E:` 等槽區。
- `RemoteDesktop.Agent` 新增系統匣常駐模式，預設啟動時直接在背景執行，不先顯示主視窗，也不顯示系統通知；可從系統匣圖示顯示主視窗、開啟設定或結束程式。
- 新增 `Agent:StartHidden` 設定，預設為 `true`，可依現場需求切換是否啟動時顯示主視窗。
- 新增 `Agent:ShowTrayIcon` 設定，預設為 `true`；若設為 `false`，Agent 會完全背景執行，不顯示系統匣圖示。
- 調整 Agent 背景模式邏輯：`StartHidden = true` 時現在會在第一個視窗建立前就阻止主視窗顯示，不再先閃出視窗再隱藏；搭配 `ShowTrayIcon = false` 時可做成完全無 UI 的背景常駐模式。

- Host 主畫面新增「裝置詳細資訊」視窗，可查看單一裝置的完整 inventory、最近盤點時間、歷史快照與變更摘要。
- 新增 inventory 匯出功能，可將單一裝置的目前盤點與變更歷史匯出為 `CSV` 與 `Excel (.xlsx)`。
- Agent 新增 `Agent:InventoryRefreshMinutes`，預設每 `360` 分鐘重新盤點一次，並透過新的 `inventory-update` 訊息將更新送回 Host / Server。
- `RemoteDesktop.Server` 與 `RemoteDesktop.Host` 的 `Memory` / `SqlServer` 裝置儲存現在都會追蹤 inventory 變更歷史；`SqlServer` 模式新增 `dbo.RemoteDesktopInventoryHistory` 保存 inventory 指紋、完整 JSON、盤點時間、記錄時間與變更摘要。
- 中央 Server 新增 `GET /api/devices/{deviceId}` 與 `GET /api/devices/{deviceId}/inventory-history`，供 Host 詳細視窗載入目前盤點與變更歷史。
- 擴充 `RemoteDesktop.SmokeTests`，新增 inventory update/history round-trip 驗證，確認 Agent 重新盤點後會覆寫最新 inventory 並追加歷史紀錄。

- Agent 現在會固定以本機主機名稱作為 `DeviceId` 與 `DeviceName`；設定表單中的這兩個欄位改為唯讀提示，不再允許手動輸入與實際註冊值脫節。
- 新增 Agent 軟硬體盤點能力：Agent 啟動時會收集 CPU、總記憶體、固定磁碟摘要、Windows 版本/組建、Office 版本，以及最近一次 Windows 更新名稱與日期，並跟隨 `hello` 註冊流程上報。
- `RemoteDesktop.Server` 與 `RemoteDesktop.Host` 的 `Memory` / `SqlServer` 裝置儲存現在都會保存 inventory profile；`SqlServer` 模式新增 `dbo.RemoteDesktopDevices.InventoryJson` 與 `InventoryCollectedAt`。
- Host 主畫面裝置清單新增硬體摘要、作業系統、Office 與最後更新欄位，讓操作端不用開 Viewer 就能先看盤點結果。
- 新增中央 inventory smoke test，驗證 Agent 上報的盤點資料可經由 `/api/devices` 正確讀回。
- 新增 `tests/RemoteDesktop.LoadTests`，可直接模擬 `300 Agent / 5 Viewer` 的中央模式壓測，並輸出 CPU、RAM、網路、WebSocket 穩定性、heartbeat timeout 與 dashboard latency 報告到 `artifacts/load-tests`。
- 完成第一輪中央模式壓測，固定情境為 `300 Agent 在線 + 5 Viewer 同時附掛 + 5 台同時串流`，Agent 預設參數維持 `8 FPS / JPEG 55 / MaxWidth 1600` 不變。
- 壓測最新結果：穩態平均 CPU `0.16%`、穩態峰值 RAM `222.24 MB`、穩態 Ingress `28.74 Mbps`、穩態 Egress `28.71 Mbps`、dashboard online event P95 `40.18 ms`、heartbeat timeout P95 `55.76 s`、WebSocket `unexpected close = 0`。
- 重構中央 Server heartbeat timeout 路徑：Agent monitor 改為依 `AgentHeartbeatTimeoutSeconds` 動態調整掃描頻率（`1-10` 秒），stale agent 改為平行回收，避免多台 timeout 時被逐台關閉拖慢。
- 修正中央 Server stale disconnect 在 `WebSocket.CloseAsync` 卡住時無法完成 repository cleanup 的問題；現在超過 2 秒會改以 `Abort()` 強制中止，仍會正確落地 `device-offline` 與 presence close。
- 擴充 `RemoteDesktop.SmokeTests`，新增中央 heartbeat timeout 情境，驗證 dashboard `device-offline` 與 `/api/devices` 的離線狀態會正確出現。
- 新增 [CAPACITY_PLAN.md](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\CAPACITY_PLAN.md)，將 `300 Agent / 5 Viewer` 壓測整理成一頁式容量規劃結論，方便交付與驗收。
- 調整交付策略：`Deploy-App.ps1` 現在會將 `RemoteDesktop.Host`、`RemoteDesktop.Agent`、`RemoteDesktop.Server` 全部發布為單檔 `win-x64 self-contained` EXE，保留外部 `appsettings.json` 與資料腳本，改善現場交付與部署便利性。
- `Clean-App.ps1` 現在會同步清掉 `users.json`、`audit-log.ndjson` 與檔案傳輸日誌，避免將執行期資料誤打進 publish / release 套件。
- 修正 Host / Agent 的 build 資訊來源，單檔發布後改用 `Environment.ProcessPath` 讀取 EXE 實際時間，不再依賴 `Assembly.Location`。
- 修正 `RemoteDesktop.Host` 的 `CentralServerUrl` 設定驗證，現在允許空值或空字串；只有真的填了內容時才要求是合法完整 URL，避免本機模式下因 DataAnnotation 驗證直接啟動失敗。
- 補齊中央 Server 的正式交付流程：新增 `deploy/scripts/Deploy-App.ps1`，可一鍵 clean、build、smoke test、UI automation、publish Host/Agent/Server，並重建 `deploy/release/current`、日期版資料夾與 zip 套件。
- `Publish-App.ps1` 現在改為可依專案指定 framework，不再假設所有專案都走 `net8.0-windows`，因此 `RemoteDesktop.Server` 也可納入同一套 publish 腳本。
- 新增 `deploy/scripts/Start-Server.cmd` 與 `deploy/scripts/Publish-Server-Launcher.cmd`，讓中央 Server 的 publish 與啟動流程與 Host / Agent 一致。
- 更新 `README.md` 與 `INSTALLATION_GUIDE.md`，補上 `deploy/publish/Server`、`Deploy-App.ps1`、`Start-Server.cmd` 與 `deploy/release` 新的交付結構。
- `Deploy-App.ps1` 現在會輸出 `release-manifest.json` 與 `release-summary.txt`，交付包可直接追蹤 commit、產生時間與 Host/Agent/Server 大小摘要。
- 新增 `deploy/scripts/Verify-Central-Release.ps1`，可直接驗證 release 套件中的 publish 版中央 Server 是否能正常啟動並回應 `/healthz`。
- 第七階段新增中央儀表板 WebSocket 推播 /ws/dashboard，RemoteDesktop.Server 會在裝置上線、離線與授權變更時主動推送 dashboard-changed 事件。
- RemoteDesktop.Host 的中央模式主畫面改成「即時推播 + 30 秒回補輪詢」，多台 Console Client 共用同一個中央狀態時不再只靠固定 5 秒 polling。
- 擴充 RemoteDesktop.SmokeTests，新增中央 dashboard push 端到端驗證，確保 Agent 註冊後可立即收到 dashboard-ready / dashboard-changed 封包。
- 修正 CHANGELOG.md、TODO.md、INSTALLATION_GUIDE.md 既有的格式瑕疵，補齊中央模式的即時推播與操作說明。
- 第八階段新增中央 Host 設定 API `/api/settings/host`，中央模式下的 Host 設定表單現在會改由 `RemoteDesktop.Server` 載入與儲存，只有 `CentralServerUrl` 仍保留在每台 Console Client 本機。
- 擴充 `RemoteDesktop.SmokeTests`，新增中央設定 API round-trip 驗證，確認管理員可讀取並更新中央 Host 設定，同時不污染本機工作目錄。

## 2026-04-10

- 新增 `src/RemoteDesktop.Server` 與 `src/RemoteDesktop.Shared`，作為「中央 Host Server + 多台 Console Client」重構的第一階段骨架。
- `RemoteDesktop.Server` 目前已可獨立啟動、提供 `/ws/agent` 與 `/healthz`，並接受 Agent `hello/heartbeat` 協定。
- 將 Agent / Viewer 通訊模型與裝置資料 DTO 搬到 `RemoteDesktop.Shared`，供後續 Server / Console Client 共用。
- 將 `RemoteDesktop.Server` 與 `RemoteDesktop.Shared` 納入 `RemoteDesktopSystem.sln` 與根目錄聚合建置流程。
- 補齊 `INSTALLATION_GUIDE.md`，將 Remote Viewer 的遠端檔案總管、遠端項目移動、下載流程與交付包路徑寫成正式操作手冊。
- 更新 `README.md`，同步反映遠端檔案總管已上線、UI automation 已覆蓋檔案總管流程，以及 `deploy/release` 交付包輸出位置。
- 第二階段新增 `ControlServer:CentralServerUrl` 設定，讓現有 `RemoteDesktop.Host` 可切到中央 Server 儀表板模式，直接透過 `/api/devices`、`/api/presence-logs` 與授權 API 顯示中央裝置狀態。
- `RemoteDesktop.Server` 新增 Console Client 所需的第一批 API 端點：`/api/devices`、`/api/presence-logs` 與裝置授權更新端點。
- `RemoteDesktop.Host` 主畫面已改成雙模式資料來源：未設定 `CentralServerUrl` 時維持本機模式；設定後改讀中央 Server。
- 第三階段新增中央 `Viewer` websocket 通道 `/ws/viewer`，讓 `RemoteDesktop.Host` 在中央模式下也能透過 Server 中繼開啟 Viewer、接收畫面串流並轉送控制命令。
- `RemoteDesktop.Host` 新增 `IRemoteViewerSessionBroker` 抽象，將本機 broker 與中央 Server viewer 通道拆開，避免 UI 與傳輸路徑耦合。
- 已實測中央 Server viewer bridge：Agent 經 `/ws/agent` 連入後，Viewer 可透過 `/ws/viewer` 收到畫面與轉送控制命令。
- 第四階段新增中央 `POST /api/auth/login`、`GET/POST/DELETE /api/users`、`GET/POST /api/audit-logs`。
- `RemoteDesktop.Host` 的登入窗、使用者管理與稽核視窗已改成面向介面，在中央模式下會自動切到 Server API，而不是使用本機 `users.json` / `audit-log.ndjson`。
- 第五階段新增中央 `ConsoleSessionTokenService`，登入成功後會簽發 access token，並由 `RemoteDesktop.Host` 保存於 `CentralConsoleSessionState`。
- 中央 `/api/devices`、`/api/presence-logs`、`/api/users`、`/api/audit-logs` 與 `/ws/viewer` 現在都需要 bearer token；管理員 API 會由 Server 端依角色回傳 `401/403`，不再信任 Console Client 傳入的身分。
- 中央 Viewer websocket 不再接受 Client 傳入 `userName/canControl` 決定權限，改由 Server 端 session role 決定 attach 身分與是否允許控制。
- `RemoteDesktop.Agent` 的 `ClipboardSyncService` 補上 Windows 剪貼簿重試機制，修正 smoke test 與實機偶發的 OLE/clipboard busy 失敗。
- 已重新驗證 `dotnet build`、`RemoteDesktop.SmokeTests`、`RemoteDesktop.UiAutomation` 與中央 Server token-authenticated API/WebSocket smoke。
- 第六階段補上中央 Viewer Session Lock 與強制接管規則：同一台裝置同時間只保留一個控制者，其餘 Viewer 自動降為僅觀看；具控制權角色可透過 Take Control 對既有控制者發起接管。
- RemoteDesktop.Server 的 /ws/viewer 現在會在 Server 端追蹤多個 Viewer session，並在控制權切換時主動推送 `viewer-mode-updated` 給受影響的 Console Client。
- RemoteDesktop.Host 的中央模式 Viewer 現在會依 Server 回傳的 session state 啟用/停用互動功能，並新增「取得控制權 / Take Control」操作。
- 擴充 RemoteDesktop.SmokeTests，新增中央多 Viewer 鎖定、觀察者被拒絕控制、強制接管後控制權移轉的端到端驗證。
- 將 `TODO.md` 中已過時的「補遠端檔案瀏覽 UI」項目移除，改為聚焦檔案總管後續強化方向。

## 2026-04-09

- 將 Host Remote Viewer 右上角的一排 Agent 操作按鈕改成 `功能` 下拉按鍵，集中收納開啟資料夾、剪貼簿同步、upload/download、全螢幕、聚焦與中斷連線。
- 將 Agent 主畫面的右上角操作入口改成 `功能` 下拉按鍵，集中提供設定、複製裝置 ID、複製 Server 位址與立即重新整理。
- 修正 Remote Viewer 手動縮放模式在串流持續刷新時反覆重新排版的問題；若遠端解析度未改變，現在會保留目前的手動縮放與捲動位置，不再一直跳回原點。
- 將 Remote Viewer 的傳輸區塊改為預設折疊，只有在 upload/download 實際開始後才展開顯示狀態、路徑與進度列。
- 為 Remote Viewer 新增下載功能，支援輸入 Agent 端檔案路徑、選擇本機儲存位置，並在完成後直接開啟本機下載資料夾。
- 調整 Agent 主畫面的設定按鈕尺寸，縮小標題列右上角按鍵佔用空間。
- 擴充 Agent / Host 檔案傳輸協議，新增 download 狀態與 chunk 傳輸路徑，同步納入稽核紀錄。
- 修正 Remote Viewer 縮放下拉在串流持續刷新時反覆同步 `SelectedIndex` 的重入問題，避免實機操作時下拉選單無法穩定挑選、看起來像一直跳出。
- 為 Remote Viewer 新增縮放下拉選單與全螢幕模式，支援「符合視窗」與 50%-200% 手動縮放、雙擊畫面切換全螢幕，以及 `F11` / `Esc` 快捷鍵。
- 再次調整 Remote Viewer 傳輸區塊高度與進度列位置，避免長檔名或長目的地路徑在繁中 UI 下壓住進度列。
- 擴充 `tests/RemoteDesktop.UiAutomation` 的 Viewer 測試，新增縮放下拉選單與全螢幕切換驗證，避免後續版面或快捷鍵回退。
- 調整 Remote Viewer 傳輸區塊版面，將狀態訊息與目的地路徑拆成兩行顯示，並下移進度列，修正上傳成功後長路徑遮住文字與進度列的問題。
- 將 Host 的上傳選檔視窗改為獨立 STA 執行緒開啟，避開 Viewer 既有 UI 執行緒上的 modal/owner 互鎖問題，修正按下「上傳檔案」後整個視窗卡住卻沒有真正開出檔案選擇器。
- 將 Viewer 的上傳按鈕改為先 `BeginInvoke` 回到 UI 訊息迴圈後再進入選檔流程，並補上同步 fallback marker，降低 click 事件與檔案對話框重入造成整個 Viewer 卡死的風險。
- Host 的登入窗、主控台、Viewer，以及 Agent 主畫面新增 build 版本與 EXE 建置時間顯示，方便現場直接辨識是否正在執行最新 publish。
- 將 Viewer 上傳入口的權限判定改為非阻塞式狀態更新與診斷日誌，不再透過 modal 對話框卡住上傳流程，並補上 `host-upload-permission-check` / `host-upload-selection-failed` 事件。
- 將 Host 檔案選擇對話框改回同步 UI 執行緒開啟，實際上傳維持背景工作執行，修正按下「上傳檔案」後只留下 `host-upload-clicked` 診斷事件、卻沒有開啟檔案選擇器的卡點。
- 修正 Viewer 檔案上傳完成後的狀態呈現，Host 端現在會顯示 Agent 實際儲存路徑，並提供「開啟資料夾」按鈕。
- 擴充 `tests/RemoteDesktop.UiAutomation`，新增 Remote Viewer 檔案上傳 UI 自動化測試，覆蓋上傳中狀態、完成後路徑顯示與開啟資料夾流程。
- 將 Agent 檔案傳輸狀態訊息補齊 `StoredFilePath`，讓 Host 可正確呈現實際落地位置。
- 將 Host 檔案上傳 chunk 降為 16 KB，並延長檔案傳輸啟動/完成等待容忍度，降低大檔案上傳時的 GC 壓力與假性逾時。
- 重寫 Agent 輸入注入流程，滑鼠改用絕對座標 `SendInput`，鍵盤改用 scan code / extended key，並補上 Win32 失敗檢查。
- Agent 新增 `highestAvailable` manifest；若未提權，主畫面會明確提示高權限視窗、UAC 與安全桌面可能拒絕接收輸入。
- Host 的檔案上傳按鈕事件補上最外層未處理例外保護，避免真機上傳異常時直接讓 Viewer 視窗崩潰。
- Host 的上傳按鈕事件改為非阻塞背景工作，避免真機上傳時出現點擊後整個 Viewer 沒反應。
- Host 的本機剪貼簿讀寫改為專用 STA 路徑執行，修正「Clipboard ... 目前的執行緒必須先設為 STA」錯誤。
- 新增 Host / Agent 檔案傳輸診斷日誌，會落地記錄 `start/chunk/complete/abort` 與失敗訊息，方便追查真機上傳問題。

## 2026-04-08

- 修正遠端檢視器上傳檔案時的卡頓問題，將 Host 端多 chunk 傳輸移出 UI 主執行緒，並降低 Agent 端 progress 回報頻率。
- 擴充 `RemoteDesktop.SmokeTests` 的檔案傳輸驗證，改為覆蓋多 chunk 上傳與節流後的 progress 回報。
- 發佈腳本改為固定先清空 `deploy/publish` 再重建精簡 `framework-dependent` 輸出，只保留最小必要相依與 `zh-Hant` 資源。
- 新增 `deploy/scripts/Clean-App.ps1`，可一鍵清理 `bin/obj`、`.dotnet`、稽核垃圾檔，必要時也能清空 publish 目錄。
- 補強 `.gitignore`，忽略本機建置快取、暫存檔與執行期產生的 `audit-log.ndjson` / `users.json`。
- 移除 `RemoteDesktop.Agent` 不需要的 `Microsoft.AspNetCore.App` 參考，改為最小化 `Microsoft.Extensions.Hosting` 相依。
- 重新產生 Agent 發佈版，移除原本殘留在 `deploy/publish/Agent` 的 `Microsoft.AspNetCore.*` DLL。
- Host 預設儲存模式改為 `Memory`，沒有 LocalDB / SQL Server 也可先啟動主控台。
- Host 設定表單新增「啟用資料庫」選項，改為只有明確啟用時才使用 `ConnectionStrings:RemoteDesktopDb`。
- 新增正式版 `InMemoryDeviceRepository`，讓無資料庫環境也能保留在線裝置與連線歷程於記憶體中。
- `/healthz` 現在會回傳目前 `persistenceMode`。
- 新增 `deploy/publish/Host` 與 `deploy/publish/Agent` 的 `win-x64 self-contained` 發佈版。
- 新增 `deploy/scripts/Start-Host.cmd` 與 `deploy/scripts/Start-Agent.cmd` 啟動腳本。
- 在桌面建立 Host/Agent 捷徑與發佈資料夾捷徑。
- 在 Windows 啟動資料夾建立 `RemoteDesktop Agent 開機啟動.lnk`。
- 重寫 `INSTALLATION_GUIDE.md`，改為目前 WinForms 架構、publish 版、捷徑與開機啟動實際說明。
- 更新 `README.md`，補上 publish、腳本與捷徑資訊。















