# RonFlow 的 AI audit trail 持久化與查詢化

## 背景

RonFlow 的 AI apply contract 一開始已能回傳 `audit_entry_id`，也能讀回單筆 audit entry。

但早期版本的實作偏向 runtime registry：

- 可追查單次操作
- 不適合跨 session、跨部署長期保存
- 不適合用多維度查詢 AI 歷程

這次的目標是把「可讀」推進到「可持久化、可查詢、可背景投影」的結構。

## 設計重點

### 1. apply 與投影解耦

AI apply 成功後不直接同步寫入 audit read model，而是先產生 audit projection message 丟到 outbox。

這讓 apply 回應維持快路徑，audit read model 由背景 consumer 慢路徑補齊。

### 2. 導入 audit projection contracts

在 application 層新增：

- `AiAuditProjectionSource`
- `AiAuditQuery`
- `IAiAuditProjectionOutbox`
- `IAiAuditReadModelStore`
- `ProcessAiAuditProjectionService`

這些型別定義了投遞、消費、查詢三段契約，避免 controller 直接耦合底層儲存細節。

### 3. `AiAuditRegistry` 轉型為 façade

`AiAuditRegistry` 不再是 `ConcurrentDictionary`，改成：

- `Record(...)`：建立 audit message 並 enqueue outbox
- `Get(...)`：由 read model 讀取單筆
- `Query(...)`：依條件查詢 read model

同時把 `sessionId`、`occurredAt` 納入 audit entry，讓後續查詢有足夠維度。

## 基礎設施實作

### In-memory（Testing）

- `InMemoryAiAuditProjectionOutbox`
- `InMemoryAiAuditReadModelStore`

測試環境可快速驗證 outbox -> consumer -> read model 的流程。

### SQLite（Localhost / 非 Testing）

新增資料表：

- `AiAuditOutbox`
- `AiAuditReadModel`

對應實作：

- `SqliteAiAuditProjectionOutbox`
- `SqliteAiAuditReadModelStore`

`AiAuditReadModel` 以 `AuditEntryId` 為主鍵，搭配 upsert，讓同一筆事件重放時不會產生重複紀錄。

## 背景消費

新增 `AiAuditProjectionBackgroundService` 定期執行 `ProcessAiAuditProjectionService.ProcessPending()`。

流程如下：

1. 讀取 outbox pending messages
2. 將 message 套用到 read model（upsert）
3. 標記 outbox message 已處理

這提供了基本冪等與可追蹤性。

## 查詢介面

AI surface 新增 `GET /api/ai/audit-entries`，可用下列條件查詢：

- `sessionId`
- `actorIdentity`
- `targetType`
- `targetId`
- `requestedChange`
- `actualDiffContains`
- `limit`

原有 `GET /api/ai/audit-entries/{auditEntryId}` 仍保留，作為單筆追查入口。

## 驗證

本次新增驗證重點：

- 整合測試：apply 後可查單筆 audit entry
- 整合測試：`/api/ai/audit-entries` 可用 session + requestedChange 過濾
- 單元測試：同一 `AuditEntryId` message 重放只保留單筆 read model（基本冪等）
- 單元測試：多條件查詢可命中正確 audit entry

## 小結

這次改動把 RonFlow 的 AI audit 從「runtime 可讀」推進到「事件投遞 + 背景投影 + 持久化查詢」：

- apply 不需要同步完成投影
- audit 可長期保存與查詢
- 查詢維度可直接支援 session / actor / target / requested change / diff 片段

這也讓後續把 audit 能力接到更完整的 governance 或可視化頁面時，有了穩定的 read model 基礎。