# 中央 Server 一頁式容量規劃結論

## 1. 結論

目前這版 `RemoteDesktop.Server` 在以下前提下，可作為第一階段正式部署基準：

- `300` 台 Agent 常駐在線
- 同時 `2-5` 台 Viewer 使用
- Agent 預設參數不變：
  - `CaptureFramesPerSecond = 8`
  - `JpegQuality = 55`
  - `MaxFrameWidth = 1600`

這個結論適用於：

- 中央 Server 單機部署
- `Memory` 儲存模式
- Viewer 同時串流數量維持低量

不代表可直接外推到：

- `300` 台同時開 Viewer
- `SqlServer` 儲存模式
- 跨 WAN / 高延遲網路
- 長時間 `24x7` burn-in 尚未驗證的極端場景

## 2. 壓測情境

- 測試名稱：`300 Agent / 5 Viewer`
- 報告位置：
  - [load-test-report.json](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\artifacts\load-tests\central_300agents_5viewers_20260411_223621\load-test-report.json)
  - [load-test-report.md](G:\codex_pg\遠端桌面\remote-desktop-system-winforms-20260408\artifacts\load-tests\central_300agents_5viewers_20260411_223621\load-test-report.md)
- Agent 在線數：`300`
- Viewer 連線數：`5`
- 同時送圖 Agent：`5`
- Viewer 串流 FPS：`8`
- frame 模擬大小：`96 KiB`
- 儲存模式：`Memory`

## 3. 量測結果

### CPU / RAM

- 穩態平均 CPU：`0.16%`
- 穩態峰值 CPU：`0.83%`
- 穩態平均 RAM：`208.75 MB`
- 穩態峰值 RAM：`222.24 MB`

### 網路

- 穩態 Ingress：`28.74 Mbps`
- 穩態 Egress：`28.71 Mbps`

### WebSocket 穩定性

- Agent 成功連線：`300 / 300`
- Viewer 成功連線：`5 / 5`
- `unexpectedAgentCloseCount = 0`
- `unexpectedViewerCloseCount = 0`
- frame relay 成功率：`100%`

### heartbeat timeout

- Probe Agent：`10`
- Offline event：`10 / 10`
- P95：`55.76 s`
- timeout 後在線裝置數：`290 / 300`

說明：

- Server 設定值是 `45 s`
- 實際觀測約 `45-55 s`
- 這是因為 timeout 偵測除了 heartbeat 門檻外，還包含 monitor 掃描粒度

### dashboard 延遲

- Online event 平均：`19.72 ms`
- Online event P95：`40.18 ms`
- Online event Max：`50.20 ms`

## 4. 容量判斷

### 可以接受的部署範圍

- `300` 台 Agent 常駐在線
- 同時 `2-5` 台 Viewer
- 偶發剪貼簿、檔案傳輸
- 多台 Console Client 共用中央 Server

### 目前不建議直接承諾的範圍

- `10+` 台同時高畫質 Viewer
- `500+` 台 Agent 尚未壓測
- `SqlServer` 模式尚未做同級壓測
- TLS / WSS 啟用後的吞吐影響尚未量測

## 5. 部署建議

### 建議機器等級

中央 Server 建議至少：

- CPU：`4 vCPU`
- RAM：`8 GB`
- 網路：`1 Gbps LAN`
- 磁碟：SSD

雖然這輪壓測顯示 CPU / RAM 佔用很低，但正式環境還會疊加：

- 稽核
- 設定持久化
- Console Client 多人操作
- 檔案傳輸
- Windows 背景服務與防毒

因此不建議把測試值當作機器最小下限。

### 網路與架構建議

- `Server` 獨立放在固定主機
- `Host` 只作為 Console Client，不與 Server 混跑在日常工作機
- `Agent` 與 `Host` 都只連中央 Server
- 內網正式環境建議固定 IP / DNS

## 6. 風險與限制

### 已知限制

- 這輪是 `Memory` 模式，不是 `SqlServer`
- Viewer 負載是 `5` 台，不是高併發觀看
- heartbeat timeout 實際觀測不是硬 `45 s`，而是約 `45-55 s`

### 尚未完成的驗證

- `SqlServer` 模式壓測
- `500+ Agent` 壓測
- 長時間 burn-in
- WAN / VPN 高延遲環境
- 啟用 TLS / `https` / `wss` 後的吞吐影響

## 7. 建議採用方式

### 可直接採用

- 第一階段內網上線
- `300` 台 Agent 量級
- 同時 `2-5` 台 Viewer

### 上線前建議補做

1. `SqlServer` 模式第二輪壓測
2. `24` 小時 burn-in
3. 正式環境網路與主機監控
4. TLS / token / secrets 的資安硬化

## 8. 交付結論

這版可以作為：

- `300 Agent / 2-5 Viewer` 的第一階段中央部署基準版

不應該被解讀成：

- 任意放大到高併發 Viewer 或更大量 Agent 而不再驗證

如果要往下一階段擴到：

- `500+ Agent`
- `10+ Viewer`
- `SqlServer` 正式模式

應先補第二輪容量驗證。
