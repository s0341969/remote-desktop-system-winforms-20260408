# Changelog

## 2026-04-08

- 新增 `RemoteDesktopSystem.sln`，可直接用 `Visual Studio 2022` 開啟整個專案。
- 將 `RemoteDesktop.Host` 從 Web 控制台重構為 Windows Forms 主控台。
- 保留 Host 背景 `Kestrel` 與 Agent `/ws/agent` WebSocket 通訊端點。
- 新增 Host WinForms 表單：
  - `LoginForm`
  - `MainForm`
  - `RemoteViewerForm`
- 將 `RemoteDesktop.Agent` 改為 Windows Forms 啟動模式，避免額外 console 視窗。
- 新增 Agent WinForms 表單 `AgentMainForm`，顯示連線狀態、最近送圖與錯誤資訊。
- 新增 `AgentRuntimeState`，整理 Agent 執行狀態與事件紀錄。
- 新增 `CredentialValidator`，集中 Host 主控台登入驗證邏輯。
- 更新 `README.md`，補上目前 WinForms 架構、VS2022 開啟方式、執行與設定說明。
- 更新 `TODO.md`，重排後續 WinForms 化與安全性待辦。

