# TODO

- [ ] 將 Host 管理帳號與 `SharedAccessKey` 改為安全儲存，避免明文留在 `appsettings.json`
- [ ] 將 Agent 包裝成可選的 Windows Service 模式，同時保留 WinForms 設定介面
- [ ] 強化遠端檢視表單，補上縮放比例切換、全螢幕、快捷鍵提示，以及遠端輸入失敗原因的 Host 端可視化回報
- [ ] 增加 Host 與 Agent 的操作日誌匯出功能
- [ ] 擴充 UI 自動化測試，納入遠端檢視視窗實際 attach、設定儲存後重新載入驗證，以及剪貼簿同步流程
- [ ] 擴充 smoke test，加入未授權 Agent、重複登入取代與 heartbeat timeout 情境
- [ ] 為設定檔持久化流程補上獨立的檔案層測試，覆蓋異常 JSON 與缺欄位回復策略
- [ ] 將 `Clean-App.ps1` 與 `Publish-App.ps1` 整合成單一部署腳本，加入 build、smoke test 與版本標記流程
- [ ] 為 Host 儲存模式切換補上更多自動化測試，覆蓋 `Memory` 與 `SqlServer` 兩種啟動路徑
- [ ] 盤點 Host 發佈輸出是否仍有可安全縮減的非必要執行時相依，避免在不影響穩定性的前提下攜帶過多 DLL


