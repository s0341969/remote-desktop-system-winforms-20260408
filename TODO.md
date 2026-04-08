# TODO

- [ ] 將 Host 管理帳號與 `SharedAccessKey` 改為安全儲存，避免明文留在 `appsettings.json`
- [ ] 將 Agent 包裝成可選的 Windows Service 模式，同時保留 WinForms 設定介面
- [ ] 將 Host 的設定畫面表單化，支援直接在 UI 編輯連線字串與控制端參數
- [ ] 強化遠端檢視表單，補上縮放比例切換、全螢幕與快捷鍵提示
- [ ] 增加 Host 與 Agent 的操作日誌匯出功能
- [ ] 清理已停用的 Razor Pages 舊檔，確認不再需要後再移除
- [ ] 補 WinForms UI 自動化測試，涵蓋登入、裝置清單刷新與遠端視窗開啟流程
- [ ] 擴充 smoke test，加入未授權 Agent、重複登入取代與 heartbeat timeout 情境
