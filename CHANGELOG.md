# 更新日誌 (Changelog)

本檔案記錄 Fortified Feature Framework 的版本變動。

---

## [未發布] — 2026-06-21

涵蓋範圍：自 `release-20260607`（2026-06-07）以來的變動。

### 新增功能

- **警報系統（Alert System）**
  新增一系列警報建築（共用 `FFF_AlertBuildingBase` 底座），包含掃描器、效果器與實體釋放等元件：
  - `CompAlertScanner`：偵測周遭威脅並觸發警報。
  - `CompAlertEffector` / `CompAlertEffector_ReleaseEntity`：警報觸發時執行效果，並可釋放實體。
  - `MapComponent_AlertCounter`：以地圖層級統計與管理警報狀態。
  - 建築受 EMP／暈眩影響時會癱瘓並停止掃描（搭配 `CompCanBeDormant`）。

- **傷害阻擋 / 反應裝甲（Damage Blocker / ERA）**
  以「閾值」為核心的防護系統，可設定阻擋次數、補充機制與爆炸反應裝甲（ERA）反擊：
  - 依傷害是否超過閾值決定完全阻擋、夾制（clamp）或穿透，並可設定是否消耗一次充能。
  - 支援來源過濾（遠程／近戰／直接傷害、武器標籤等限制條件）。
  - ERA 反擊：在受擊、消耗充能、超過／低於閾值等條件下對目標造成反向傷害。
  - 配套 Gizmo 狀態顯示、戰鬥日誌條目與裝備統計面板。

- **持續傷害器（Continuous Damager）**
  類熱熔裝置：啟動後朝前方持續造成範圍傷害，維持一段時間後自我銷毀。包含啟用元件、放置範圍 PlaceWorker 與啟動使用效果。

- **地下變電所（Substation）**
  新增地下變電所建築（機櫃與壁掛面板）：
  - 運作時提供 3000 W 電力。
  - 可被駭入以關閉（關閉時有風險），手動重啟則無需技能檢定。
  - 受 EMP 影響時停機。

- **內建電池（Internal Battery）**
  `CompPowerTrader_InternalBattery`：自供電行為的內建電池元件。

- **旋轉效果器（`Comp_EffectorRotational`）**
  支援依建築朝向自動旋轉的效果器偏移，提供逐方向偏移與自動旋轉兩種模式。

- **電源顏色切換（`CompPowerColorSwitch`）**
  依電源狀態切換 glower 發光顏色。

### 改進與修正

- `ActiveProtectionSystem`（主動防護系統）攔截邏輯調整。
- `DropMech`（投放機械）陣營關係處理修正。
- `SelfRepairMode`（自我修復模式）驗證強化。
- 錐形爆炸（Cone Explosive）：攔截 `Faction.TryAffectGoodwillWith`，避免友好 NPC 陣營因錐形爆炸對玩家產生好感度懲罰。
- 裝甲板背心（PlateArmorVest）補充任務（`JobDriver_Replenish`）調整。
- `HumanlikeMech` 相關調整。
- 黑光判定（`DarklightUtility.IsDarklight`）與電力連接（`ThingDef ConnectToPower`）補丁。
- 多處新增 null 檢查與防護判斷，提升穩定性。
- 移除自訂的 ToggleSubstation 任務，改用原版 hack 流程。

### 其他

- 新增正體中文、簡體中文、英文在地化字串（警報系統、傷害阻擋、持續傷害器、變電所、內建電池等）。
- 重新編譯 `Fortified.dll`；更新 CombatExtended 相容組件；新增 `build.bat` 建置腳本。

---

## 歷史版本

完整版本標記（release tag）列表：

| 版本標記 | 日期 |
| --- | --- |
| release-20260607 | 2026-06-07 |
| release-20260531 | 2026-05-31 |
| release-20260525 | 2026-05-25 |
| release-20260524 | 2026-05-24 |
| release-20260412 | 2026-04-12 |
| release-20260320 | 2026-03-20 |
| release-20260310 | 2026-03-10 |
| release-20260305 | 2026-03-05 |
| release-20260302 | 2026-03-02 |
| release-20260223 | 2026-02-23 |
| release-20260210 | 2026-02-10 |
| release-20251207 | 2025-12-07 |
| release-20251204 | 2025-12-04 |
| release-20251123 | 2025-11-23 |
| release-20251109 | 2025-11-09 |
| release-20251027 | 2025-10-27 |
| release-20251020 | 2025-10-20 |
| release-20251018 | 2025-10-18 |
| release-20250930 | 2025-09-30 |
