# Changelog

## 2026-04-09

- 修正 Viewer 檔案上傳完成後的狀態呈現，Host 端現在會顯示 Agent 實際儲存路徑，並提供「開啟資料夾」按鈕。
- 擴充 `tests/RemoteDesktop.UiAutomation`，新增 Remote Viewer 檔案上傳 UI 自動化測試，覆蓋上傳中狀態、完成後路徑顯示與開啟資料夾流程。
- 將 Agent 檔案傳輸狀態訊息補齊 `StoredFilePath`，讓 Host 可正確呈現實際落地位置。
- 將 Host 檔案上傳 chunk 降為 16 KB，並延長檔案傳輸啟動/完成等待容忍度，降低大檔案上傳時的 GC 壓力與假性逾時。
- 重寫 Agent 輸入注入流程，滑鼠改用絕對座標 `SendInput`，鍵盤改用 scan code / extended key，並補上 Win32 失敗檢查。
- Agent 新增 `highestAvailable` manifest；若未提權，主畫面會明確提示高權限視窗、UAC 與安全桌面可能拒絕接收輸入。
- Host 的檔案上傳按鈕事件補上最外層未處理例外保護，避免真機上傳異常時直接讓 Viewer 視窗崩潰。

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




