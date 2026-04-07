# TODO

- [ ] 修正 Host 與 Razor Pages 內仍存在的亂碼文字，統一為 UTF-8 與繁體中文
- [ ] 將 `AdminPassword` 與 `SharedAccessKey` 改為部署期機密注入，不再保留版本庫中的預設值
- [ ] 將 `RemoteDesktop.Agent` 封裝為 Windows Service，補齊開機啟動與背景常駐流程
- [ ] 補上正式環境部署文件，包含 HTTPS、反向代理、防火牆與憑證配置
- [ ] 增加管理者審計紀錄與更完整的遠端操作追蹤能力
