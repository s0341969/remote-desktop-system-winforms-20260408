# Changelog

## 2026-04-08

- 修正 Host/Agent 專案建置輸出未複製 `appsettings.json` 的問題，避免直接執行 EXE 時讀不到設定。
- 新增 Host 設定表單 `HostSettingsForm`，可直接在 WinForms UI 內編輯主控台設定與資料庫連線字串。
- 新增 Agent 設定表單 `AgentSettingsForm`，可直接在 WinForms UI 內編輯控制端 URL、裝置識別與影像參數。
- 新增 `HostSettingsStore` 與 `AgentSettingsStore`，集中 `appsettings.json` 讀寫與資料驗證。
- Host 主畫面新增設定按鈕，Agent 主畫面新增設定按鈕。
- 清除 `src/RemoteDesktop.Host/Pages` 與 `src/RemoteDesktop.Host/wwwroot` 已停用的 Razor Pages 舊碼。
- 更新 `RemoteDesktop.Host.csproj`，移除只為舊 Razor Pages 殘留的設定。
- 新增 `tests/RemoteDesktop.UiAutomation`，驗證 Host 登入、Host 設定、Agent 設定、Host 主畫面與 Agent 主畫面。
- 將 `RemoteDesktop.UiAutomation` 專案加入 `RemoteDesktopSystem.sln`。
- 修正 `AgentOptions` 預設 `ServerUrl`，統一為 `http://localhost:5106`。
- 更新 `README.md`，補上設定表單、UI 自動化測試與已移除的舊 Web UI 說明。
- 更新 `TODO.md`，移除已完成項目並重排後續待辦。
