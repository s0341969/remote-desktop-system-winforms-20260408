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
  - Host 的單檔 `win-x64 self-contained` 發佈版。
- `deploy/publish/Agent`
  - Agent 的單檔 `win-x64 self-contained` 發佈版。
- `deploy/publish/Server`
  - Server 的單檔 `win-x64 self-contained` 發佈版。
- `deploy/scripts`
  - 發佈、清理與整包交付腳本。
- `tests/RemoteDesktop.SmokeTests`
  - 核心通訊 smoke test。
- `tests/RemoteDesktop.UiAutomation`
  - WinForms UI 自動化測試。
- `tests/RemoteDesktop.LoadTests`
  - 中央模式壓測工具。
  - 可模擬 300 Agent 在線、5 Viewer 同時附掛，並輸出 CPU、RAM、網路、WebSocket 穩定性、heartbeat timeout 與 dashboard 延遲報告。
- [CAPACITY_PLAN.md](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\CAPACITY_PLAN.md)
  - 一頁式容量規劃結論。
  - 彙整 `300 Agent / 5 Viewer` 壓測結果、部署假設、容量判斷與上線建議。
- `RemoteDesktopSystem.csproj`
  - 根目錄聚合建置檔，用來一次建置主要執行專案。

## 本次整理重點

- 補齊 Host 與 Agent 的完整設定表單，改由 UI 編輯 `appsettings.json`。
- Agent 啟動時現在會主動收集並上報軟硬體盤點，包含 CPU、總記憶體、固定磁碟摘要、Windows 版本與組建、Office 版本，以及最近一次 Windows 更新名稱與日期。
- Agent 的硬體摘要與作業系統版本盤點現在已補多層 fallback；若部分電腦上的 WMI `Win32_Processor`、`Win32_ComputerSystem` 或 `Win32_OperatingSystem` 查詢失敗，會改用 registry、`Environment.OSVersion`、`RuntimeInformation.OSDescription` 與 `GlobalMemoryStatusEx` 補抓，避免主控台出現空白或整欄未知。
- Agent 現在會強制將 `DeviceId` 與 `DeviceName` 正規化為本機 `Environment.MachineName`，避免現場手動輸入造成重複或命名不一致。
- 裝置盤點會沿著 Agent `hello` 註冊流程一路帶進 Host / Server，並可同時儲存在記憶體模式與 `SqlServer` 模式；資料庫模式會寫入 `dbo.RemoteDesktopDevices.InventoryJson` 與 `InventoryCollectedAt`。
- Host 主畫面現在會直接顯示裝置的硬體摘要、作業系統、Office 與最後更新摘要，方便操作端先做盤點，不必先開 Viewer。
- Host 主畫面新增「裝置詳細資訊」，可展開查看目前裝置的完整 inventory 與歷史快照，不必再只看摘要欄位。
- Host 主畫面的「已連線裝置」與「在線紀錄」表格現在支援點擊所有欄位標題排序，並在背景自動更新時保留目前排序、選取列與卷動位置，避免 GridView 在輪詢刷新時一直跳動。
- Host 主畫面的時間欄位現在直接顯示 `yyyy-MM-dd HH:mm:ss zzz`，保留資料本身的 offset；`Memory` 與 `SqlServer` 模式的新寫入時間也改用主機本地 `DateTimeOffset.Now`，避免資料庫與畫面一邊是 `+00:00`、另一邊是本機時間造成混淆。
- Host 主畫面新增「查詢主機名稱 / IP」功能，可直接用電腦名稱、主機名稱、裝置名稱、裝置 ID 或 IP 位址快速篩選裝置與在線紀錄。
- Host / Server 現在會記錄 Agent 連入時的遠端 IP 位址；主畫面的「已連線裝置」與「在線紀錄」都會顯示 IP 欄位，`Memory` 與 `SqlServer` 模式也都會同步保存。
- `RemoteDesktopAgentPresenceLogs` 的寫入規則已調整為兩段式抑制：只有狀態真的從離線轉為上線時才新增一筆；若同一台裝置仍在在線期間內只是重連或重新註冊，系統會沿用尚未關閉的同一筆紀錄，改為更新 `LastSeenAt`、IP、版本與主機資訊。當 Agent 後續離線時，若 `DisconnectReason` 與同裝置上一筆已關閉紀錄相同，系統會覆寫既有紀錄並移除本次新筆數；只有離線原因改變時才會保留新一筆在線紀錄。
- 中央模式主控台收到大量 dashboard push 事件時，現在會先做短時間節流再刷新，並在 Grid 重綁資料時暫停重繪，降低多台 Agent 在線時的整頁閃爍感。
- Host 主畫面的 header 與摘要資訊區已重新整理版面，長標題、build 資訊、Server URL 與健康檢查位址改為可自動換行或省略，不再互相遮蓋；兩個 Grid 也改為單格選取，避免每次點欄位都整列反白。
- Host 主畫面的 `已連線裝置` 與 `在線紀錄` GridView 現在會自行處理 `Ctrl + C` 單格複製，直接將目前欄位值寫入剪貼簿，不再走 DataGridView 內建 OLE 複製路徑，因此不會再跳出 `Current thread must be set to STA` 的例外。
- Host 主畫面在 `CellSelect` 模式下開啟 Viewer 前，現在會以目前儲存格所在列為準抓取裝置，並再向資料來源即時確認一次最新在線/授權狀態，避免畫面顯示「在線」卻因舊選取列快取誤判成離線。
- Host 主畫面的單台裝置查詢列已改回固定顯示在「已連線裝置」上方，不再掛在主標題區，因此不會因 header 重排或長 build 字串而看起來像消失。
- 角色權限已調整為：`Administrator` 可開啟並完整控制 Viewer，且可使用上傳/下載；`Operator` 也可開啟並控制 Viewer、可同步剪貼簿與執行既有遠端控制操作，但不可使用上傳/下載檔案；`Viewer` 仍維持只能開啟 Viewer 觀看；只有 `Administrator` 可管理使用者、設定、稽核與裝置授權。
- 新增 inventory 匯出功能，可將單一裝置的目前盤點與變更歷史輸出成 `CSV` 或 `Excel (.xlsx)`；匯出目的地視窗現在改走獨立 STA 對話框執行緒，實際寫檔也會在背景執行，避免「裝置詳細資訊」視窗在匯出時整個卡住。
- Agent 現在會依 `Agent:InventoryRefreshMinutes` 定期重新盤點，預設每 `360` 分鐘重新收集一次；只有 CPU、記憶體、磁碟、OS、Office 或最後更新摘要真的改變時，Host / Server 才會留下變更歷史，單純盤點時間 `CollectedAt` 更新不再新增歷史。
- Agent 的畫面擷取現在若遇到 Windows Server / RDP session 切換造成的互動桌面暫時不可用，會保留 WebSocket 連線並持續重試，不再因 `CopyFromScreen` 類型的擷取例外讓 Agent 整條連線中斷、主控台反覆在線/離線跳動。
- Agent 現在也會把「整張幾乎全黑的 frame」視為互動桌面不可用，而不是照常送到 Viewer；Viewer 若持續收不到有效畫面，會改顯示桌面不可擷取的明確狀態，不再只剩一整片黑畫面。
- Agent 新增 `Agent:AutoRecoverInteractiveSessionOnWindowsServer`，預設為 `true`；當 Windows Server 的微軟遠端桌面關閉後導致互動桌面不可擷取時，Agent 會嘗試使用 `tscon` 將目前 session 切回 console，讓 Viewer 有機會自動恢復畫面，不必一直保持 mstsc 視窗開啟。
- `SqlServer` 模式新增 `dbo.RemoteDesktopInventoryHistory`，會保存 inventory 指紋、完整 JSON、盤點時間、記錄時間與變更摘要；`Memory` 模式也會同步保留最近歷史。
- `SqlServer` 模式新增 `dbo.RemoteDesktopDevices.RemoteIpAddress` 與 `dbo.RemoteDesktopAgentPresenceLogs.RemoteIpAddress`，用來持久化 Agent 遠端 IP，舊資料庫也會在啟動時自動補欄位。
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
- `deploy/scripts/Clean-App.ps1` 現在也會清掉執行期產生的 `users.json`、`audit-log.ndjson` 與檔案傳輸日誌，避免把現場資料誤帶進新的交付包。
- `deploy/scripts/Publish-App.ps1` 現在會先清空輸出目錄，再以最小必要相依與 `zh-Hant` 資源重建 publish 版，避免舊的 self-contained DLL 殘留。
- 修正檔案上傳流程造成 Viewer 卡頓的問題：Host 上傳改為背景傳輸，Agent 端進度訊息改為節流回報，並將單一 chunk 降到 16 KB，避免大檔案傳輸時產生過大的 Base64 暫存字串。
- Host 的上傳按鈕事件現在有最外層例外保護；若實際環境仍遇到傳輸異常，會顯示錯誤訊息而不是直接讓整個 Viewer 當掉。
- 上傳按鈕事件改為非阻塞背景工作，避免 WinForms 事件本身長時間佔住 UI 訊息迴圈。
- Viewer 在上傳完成後會直接顯示 Agent 端實際儲存位置，並提供「開啟資料夾」按鈕快速打開目的資料夾。
- Upload 路徑的權限不足情境改為非阻塞式狀態更新與診斷日誌，不再透過可能被遮住的 modal 對話框卡住整個 Viewer。
- Upload 按鈕現在會先回到 UI 訊息迴圈再開啟選檔流程，並額外寫入同步 fallback marker，避免 click 事件與 modal 對話框重入時整個 Viewer 無回應。
- Host 的檔案選擇器現在改由獨立 STA 執行緒開啟，不再依賴 Viewer 當前 UI 執行緒的 modal 狀態，避免選檔視窗無法彈出導致上傳流程卡死。
- Host 的上傳、下載、遠端檔案總管與目的資料夾挑選視窗現在都會帶著 Viewer owner handle 並主動前置，避免切換槽區或選擇檔案時被遠端檢視 UI 蓋住。
- Viewer 的下載流程已再修正：下載按鈕現在和上傳相同，會先回到 UI 訊息迴圈再進入選檔流程；下載目的地 `SaveFileDialog` 也改回優先由 Host UI 執行緒顯示，避免實機上點下載後整個 Viewer 卡住、且沒有跳出存檔視窗。
- Agent 的檔案下載讀取路徑已改成明確允許 `ReadWrite/Delete` 共享開檔；遠端檔案即使仍被 Word、Excel、記事本等程式保持開啟，只要對方不是用完全獨占鎖住，Viewer 也可直接下載目前內容。
- Host 的本機下載落地流程也已改成使用唯一暫存檔；若使用者選定的本機目的檔剛好被占用或無法覆蓋，系統會自動遞補 ` (1)`、` (2)` 等檔名，避免同名目標或殘留 `.downloading` 檔讓整個下載失敗。
- 遠端檔案總管與移動目的地資料夾挑選器現在都新增「槽區 / Drive」下拉，可直接在 `C:`、`D:`、`E:` 等磁碟根目錄間切換，不再只能手動改遠端路徑文字框。
- Viewer 的傳輸區塊改為分開顯示「狀態」與「目的地路徑」，並重新安排進度列位置，避免長路徑把文字與進度列擠在一起。
- Viewer 新增縮放下拉選單，可在「符合視窗」與 50%-200% 間切換；手動縮放時會改為可捲動檢視，方便放大局部畫面。
- 縮放下拉選單的同步邏輯已避免在串流刷新期間反覆改寫 `SelectedIndex`，修正實機操作時下拉選單無法穩定挑選、看起來像一直跳出的問題。
- 手動縮放時，Viewer 現在不會在每一張串流畫面都重新排版；若遠端解析度沒有改變，會保留目前的手動縮放與捲動位置，不再一直跳回左上角。
- Viewer 新增全螢幕模式，可用按鈕或雙擊遠端畫面切換，並支援 `F11` 進入/退出與 `Esc` 離開全螢幕。
- Viewer 的 `功能` 下拉新增「切換登入畫面」，可要求 Agent 將遠端 Windows 切到登入/鎖定畫面；由於 Windows 不允許一般桌面程式直接注入標準 `Ctrl + Alt + Del`，實際採用 `LockWorkStation()` 提供穩定可用的等效行為。
- Viewer 的傳輸區塊預設折疊，只有在實際 upload/download 開始後才展開並顯示狀態、路徑與進度列。
- Viewer 新增遠端檔案總管，可直接瀏覽 Agent 端資料夾、移動遠端項目，並從總管內選取檔案下載到 Host。
- Host Viewer 右上角的 Agent 操作已改成 `功能` 下拉按鍵，集中收納開啟資料夾、剪貼簿同步、upload/download、全螢幕、聚焦 Viewer 與中斷連線。
- Host 的登入窗、主控台、Viewer，以及 Agent 主畫面現在都會顯示 build 版本與 EXE 建置時間，方便直接確認目前執行中的是否為最新發佈版。
- 版本號現在改為 repo 級別集中管理，Host / Agent / Server 每次 build / publish 都會自動產生新的四段版號 `主版.次版.yyDDD.HHmm`；Agent 上報給主控台的 `Agent 版本` 也改用同一個 build 版號來源，方便直接在主控台辨識目前實際部署版本。
- Agent 主畫面的操作入口已改成右上角 `功能` 下拉按鍵，集中提供設定、複製裝置 ID、複製 Server 位址與立即重新整理。
- Agent 現在支援系統匣常駐模式，預設啟動時會直接在背景執行，不先顯示主視窗，也不顯示系統通知；可依 `Agent:ShowTrayIcon` 決定是否顯示系統匣圖示。
- `Agent:StartHidden` 預設為 `true`，若需要現場顯示主視窗，可在 `appsettings.json` 改成 `false`。
- `Agent:ShowTrayIcon` 預設為 `true`；若改成 `false`，Agent 會完全背景執行，不會出現在系統匣。
- 若同時設為 `StartHidden = true` 與 `ShowTrayIcon = false`，Agent 會以完全無 UI 的背景模式執行；本機端不會有主視窗、工作列或系統匣入口。
- 第一階段新增 `RemoteDesktop.Server` 與 `RemoteDesktop.Shared`，把中央 Host Server 所需的通訊契約、裝置儲存與 Agent WebSocket 通道獨立出來，為後續多主控台 Console Client 做準備。
- 第二階段讓 `RemoteDesktop.Host` 可透過 `ControlServer:CentralServerUrl` 切換成中央 Server 儀表板模式；此模式下主畫面會改抓中央 Server 的裝置清單、在線紀錄與授權更新，Viewer、遠端畫面串流與 Viewer 指令轉送也已改由中央 Server websocket 中繼。
- 第七階段補上中央儀表板 WebSocket 推播 `/ws/dashboard`，中央模式的 Host 主畫面改為「事件推播 + 低頻輪詢回補」；裝置上線、離線與授權異動會即時刷新，多台主控台不再只靠固定 5 秒輪詢。
- Host / Server 的 Agent WebSocket 關閉路徑現在會容忍對端未完成 close handshake 的直接斷線；裝置重連取代舊連線或網路中斷時，會改記錄為可預期的 warning/資訊並持續完成 presence cleanup，不再把正常重連噴成 `Agent WebSocket processing failed`。
- Agent socket 關閉時若底層已經先被對端或 framework dispose/abort，現在也會視為可預期結束而非 timeout 警告，避免日誌反覆出現 `Closing agent socket timed out ... ObjectDisposedException` 的假警報。
- 第八階段補上中央 Host 設定 API `/api/settings/host`，中央模式下的 Host 設定表單會改走 Server 儲存；只有 `CentralServerUrl` 仍保留在每台 Console Client 本機，作為該主控台要連哪一台中央 Server 的入口。
- 中央 Server 在 `PersistenceMode = SqlServer` 時，現在不只裝置與在線紀錄會進資料庫，`使用者帳號`、`Host 設定` 與 `稽核紀錄` 也都會寫入 MSSQL；其中 Host 設定會以資料庫為主，並同步鏡像回 `appsettings.json` 供下次重啟載入，而使用者帳號若資料庫為空時會先自動匯入既有 `users.json`，避免現場帳號遺失。
- 補上 `tests/RemoteDesktop.LoadTests`，可直接壓測中央 `RemoteDesktop.Server` 在 `300 Agent / 5 Viewer` 情境下的 CPU、RAM、網路、WebSocket 穩定性、heartbeat timeout 與 dashboard latency。
- Server heartbeat timeout 路徑已重構：
  - Agent monitor 掃描頻率改為依 `AgentHeartbeatTimeoutSeconds` 動態調整，區間 `1-10` 秒。
  - stale agent disconnect 現在改為平行回收，避免多台 timeout 時被逐台 `CloseAsync` 拉長。
  - 若 agent socket 在 graceful close 超過 2 秒仍未完成，Server 會改用 `Abort()` 強制中止，並持續完成 repository cleanup 與 dashboard `device-offline` 推播。
- 壓測最新基準結果：
  - 報告位置：`artifacts/load-tests/central_300agents_5viewers_20260411_223621`
  - 300 Agent 在線、5 Viewer 同時附掛、其中 5 台同時串流 8 FPS
  - 穩態平均 CPU：`0.16%`
  - 穩態峰值 RAM：`222.24 MB`
  - 穩態網路吞吐：Ingress `28.74 Mbps` / Egress `28.71 Mbps`
  - WebSocket 穩定性：`300` Agent、`5` Viewer 全部成功連線，`unexpected close = 0`
  - dashboard online event P95：`40.18 ms`
  - heartbeat timeout P95：`55.76 s`
  - timeout probe 後在線裝置數：`290 / 300`
  - 說明：heartbeat timeout 設定值為 `45 s`，實際觀測值包含 monitor 掃描粒度，因此約為 `45-55 s`
- 發佈流程已補齊中央 Server：`Publish-App.ps1` 現在支援依專案指定 framework，`Deploy-App.ps1` 可一鍵 clean、build、測試、publish Host/Agent/Server，並重建 `deploy/release/current`、日期版資料夾與 zip 交付包。
- 新增 `deploy/scripts/Start-Server.cmd` 與 `deploy/scripts/Publish-Server-Launcher.cmd`，讓中央 Server 也能走與 Host/Agent 一致的交付與啟動流程。
- `Deploy-App.ps1` 現在會在 `deploy/release/current` 產生 `release-manifest.json` 與 `release-summary.txt`，交付包可直接追蹤對應 commit、產生時間與 Host/Agent/Server 封裝大小。
- `deploy/scripts/Verify-Central-Release.ps1` 可直接驗證 release 套件是否完整，並啟動 publish 版 `RemoteDesktop.Server.exe` 檢查 `/healthz`。
- 修正 Host 啟動時 `CentralServerUrl` 的驗證邏輯：現在允許空值或空字串，只有真的填了內容時才要求為合法的完整 `http/https/ftp` URL，避免現場未設定中央模式時直接因 DataAnnotation 驗證失敗而無法啟動。
- `Deploy-App.ps1` 現在會將 Host / Agent / Server 全部發布為單檔 `win-x64 self-contained` EXE；DLL 與 .NET runtime 會內嵌到主執行檔，保留外部 `appsettings.json` 與資料腳本方便現場設定。
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
- `tests/RemoteDesktop.SmokeTests` 現在新增 Agent inventory round-trip 驗證，確認中央模式 `/api/devices` 真的會回傳 CPU、OS、Office 等盤點資料，而不是只在 Agent 端記憶體暫存。
- `tests/RemoteDesktop.SmokeTests` 現在也會驗證 inventory 定期更新與歷史追蹤：Agent 重新上報盤點後，中央 `/api/devices/{deviceId}` 會回最新資料，`/api/devices/{deviceId}/inventory-history` 會回傳變更紀錄。

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
- 這些 publish 版現在已是單檔自帶 runtime，不需要另外安裝 `.NET Desktop Runtime`。
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

### Load Test

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\RemoteDesktop.LoadTests\RemoteDesktop.LoadTests.csproj
```

輸出：
- `artifacts/load-tests/<timestamp>/load-test-report.json`
- `artifacts/load-tests/<timestamp>/load-test-report.md`

目前固定情境：
- `300` Agent 在線
- `5` Viewer 連線
- `5` 台 Agent 同時串流
- Agent 預設參數不變：
  - `CaptureFramesPerSecond = 8`
  - `JpegQuality = 55`
  - `MaxFrameWidth = 1600`

量測項目：
- Server CPU / RAM
- Server 估算 ingress / egress 網路吞吐
- WebSocket 穩定性
- heartbeat timeout
- dashboard latency

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
  -Framework 'net8.0-windows' `
  -RuntimeIdentifier 'win-x64' `
  -SelfContained $true `
  -PublishSingleFile $true `
  -EnableCompressionInSingleFile $true `
  -IncludeNativeLibrariesForSelfExtract $true

& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Publish-App.ps1 `
  -ProjectRelativePath 'src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj' `
  -OutputRelativePath 'deploy\publish\Agent' `
  -ExecutableName 'RemoteDesktop.Agent.exe' `
  -Framework 'net8.0-windows' `
  -RuntimeIdentifier 'win-x64' `
  -SelfContained $true `
  -PublishSingleFile $true `
  -EnableCompressionInSingleFile $true `
  -IncludeNativeLibrariesForSelfExtract $true

& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Publish-App.ps1 `
  -ProjectRelativePath 'src\RemoteDesktop.Server\RemoteDesktop.Server.csproj' `
  -OutputRelativePath 'deploy\publish\Server' `
  -ExecutableName 'RemoteDesktop.Server.exe' `
  -Framework 'net8.0' `
  -RuntimeIdentifier 'win-x64' `
  -SelfContained $true `
  -PublishSingleFile $true `
  -EnableCompressionInSingleFile $true `
  -IncludeNativeLibrariesForSelfExtract $true
```

### 一鍵部署

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\scripts\Deploy-App.ps1
```

### 驗收交付包

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\deploy\release\current\Scripts\Verify-Central-Release.ps1
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


















