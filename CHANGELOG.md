# Changelog

## 2026-04-08

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
