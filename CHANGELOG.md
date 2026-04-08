# Changelog

## 2026-04-08

- 抽出 `RemoteDesktopHostCompositionExtensions`，集中 Host 服務註冊與端點映射，降低 `Program.cs` 耦合。
- 修正 Agent 預設 `ServerUrl`，由錯誤的 `http://localhost:5000` 改為 `http://localhost:5106`，與 Host 預設值一致。
- 修正 Host 對 Agent WebSocket 指令送出缺少並發保護的問題，避免多個控制事件同時呼叫 `SendAsync`。
- 修正 Agent 背景連線迴圈的 task 協調與取消流程，避免只停止第一條子迴圈造成資源狀態不一致。
- 優化 Agent heartbeat，改用最近一次畫面尺寸資訊，避免額外重複截圖。
- 優化 Host 與 Agent 的 WebSocket 訊息讀取，改為使用 `ArrayPool<byte>` 降低配置壓力。
- 修正 `AgentRuntimeState` 的跨執行緒讀寫方式，改為 snapshot 模式供 WinForms UI 顯示。
- 修正 Host 儀表板定時更新失敗時會重複跳出錯誤對話框的問題。
- 修正遠端檢視視窗在關閉或連線不穩時的畫面更新與指令傳送例外處理。
- 新增 `tests/RemoteDesktop.SmokeTests`，驗證 Host 與 Agent 的核心連線、畫面傳送與 Viewer 指令轉送。
- 將 smoke test 專案加入 `RemoteDesktopSystem.sln`。
- 更新 `README.md`，補上本次重構內容、預設連線設定與 smoke test 執行方式。
- 更新 `TODO.md`，調整後續待辦重點。
