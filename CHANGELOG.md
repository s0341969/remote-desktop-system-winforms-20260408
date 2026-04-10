# Changelog

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









