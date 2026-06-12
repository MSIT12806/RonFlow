# RonFlow 技術結構亮點

這份文件不是 roadmap，也不是願望清單。

它的目的，是整理 RonFlow 目前已經體現、正在成形、或下一步值得展現的技術結構，讓人可以快速理解這個系統有哪些工程層面的設計亮點。

對應的實踐細節文章整理在 [RonFlow Tech](../ronflow-tech/README.md)。
這份文件負責高層亮點索引；`ronflow-tech` 則負責把亮點展開成技術實踐、設計取捨與落地細節。

## 已體現

- [x] SDD, ATDD, TDD
    - spec-first + acceptance-first 的 Outside-In 開發流程
- [x] Clean Architecture
    - domain / application / infrastructure 分層
- [x] DDD
- [x] CQRS
- [x] office system level 通知
    - Web Push + Service Worker 整合
- [x] background service
- [x] RonFlow 與 RonAuth supporting domain 的結合互動
    - 以前端 RonAuth SDK + 後端 JWT trust 建立跨 bounded context 的協作邊界
- [x] Testing-only HTTP fault injection
    - 前端 E2E 仍然打真實 API，但可由後端 middleware 受控地回傳指定 status code、message 與 delay
- [x] 多人協作設計
    - 多人協作下的 concurrency control、即時協作
- [x] 壓力測試與 observability
- [x] workflow state 與 lifecycle state 雙狀態軸設計
- [x] 活動紀錄 / audit trail 雛形
- [x] reporting / projection（第一批）
    - 已完成 workflow throughput 報表的第一批 projection slice，包含 event-like source、outbox、background consumer、projection store、query API 與 reports page
    - 已完成 Task Aging 報表，利用 task activity timeline 在 query side 計算目前 state 的停留時間，並可直接從報表開啟 Task Detail Drawer
    - 已完成 Cycle Time / Lead Time 報表，利用 createdAt、completedAt 與 activity timeline 在 query side 計算統計值與資料不足狀態
    - 實踐細節見 [RonFlow 的 reporting / projection 實踐](../ronflow-tech/ronflow-reporting-projection.md)
- [ ] SQLite-to-Git persistence sync
    - 單一使用者、多裝置的 personal sync 模式
    - runtime SQLite 與獨立 Git repository 之間的 snapshot / commit / push 流程
    - spec: [RonFlow Database Git Sync Spec](./ronflow-database-git-sync-spec.md)
- [x] Task mutation lock policy 與 aggregate execution result
    - lock 規則不再散落在各個 command service 手寫判斷，而是由 domain 中的 `TaskMutationLockPolicy` 定義各 mutation 的 lock requirement
    - application 層的 `TaskMutationGuard` 將 runtime lock 狀態轉成 `TaskMutationAuthorization`
    - `task.UpdateDetails(...)`、`task.ReplaceSubtasks(...)`、`task.ChangeState(...)` 等 aggregate mutation method 直接回傳 execution result，其中包含 `Locked`，讓 lock 成為 mutation contract 的一部分，而不是額外記得補的 cross-check
    - 實踐細節見 [RonFlow 的多人協作 / 即時同步設計](../ronflow-tech/ronflow-real-time-collaboration.md)
- [x] AI interaction surface（RonFlow v0.3 baseline）
    - spec: [RonFlow AI Interaction Surface Spec](./ronflow-ai-interaction-surface-spec.md)
    - 已提供 AI bootstrap、glossary、capabilities manifest、workflow guidance，讓 AI 能先理解系統、名詞與可用操作
    - 已提供 read-first summary：session summary、project summary、board summary、current work summary、task detail summary，讓 AI 先縮小 scope 與 target 再寫入
    - 已提供 AI apply contract：create / update / move / reorder / lifecycle / checklist 等 Project / Task 操作可經由正式 contract 寫入，並回傳可追溯的 apply result
    - 已提供 AI audit entry，讓人類可追查 AI actor、target、requested change 與 actual diff
    - 結構化 DoD 已承接到 AI 工作流程：task detail summary 會列出 checklist，`check_task_subtask` / `uncheck_task_subtask` 讓 AI 逐項完成，而不是只宣告 task 完成
    - 實踐細節見 [RonFlow 的 AI discovery surface](../ronflow-tech/ronflow-ai-discovery-surface.md) 與 [RonFlow 的 AI interaction surface](../ronflow-tech/ronflow-ai-interaction-surface.md)

## 可視需求深化

- [x] AI audit trail 持久化與查詢化
    - AI apply 產生的 audit message 已導入 outbox，由 background consumer 投影到正式 audit read model
    - 已提供 `GET /api/ai/audit-entries` 查詢介面，支援 session、actor、target、requested change 與 actual diff 片段等維度
    - 實踐細節見 [RonFlow 的 AI audit trail 持久化與查詢化](../ronflow-tech/ronflow-ai-audit-projection-read-model.md)

## 下一批值得展現

### 一定要做

- [ ] 細緻授權與操作政策
    - 目前已完成 RonFlow / RonAuth supporting domain 整合；下一步應聚焦於 Project / Task operation level 的 authorization、RBAC、policy-based access control
- [ ] 資料庫遷移與 schema evolution
- [ ] 資料切片
    - 聚焦資料量成長後的 partition / archive / retention / query performance，不與 tenant 權限邊界混在一起
- [ ] 多租戶 / workspace 邊界
    - 聚焦 tenant / workspace isolation、membership、role 與跨 workspace 操作規則，不直接等同資料庫分片
- [ ] html editor / markdown editor
- [ ] 檔案附件 / 物件儲存
- [ ] 正式部署與 release 流程
    - Docker / environment config / health check / migration / rollback / smoke test，而不是只描述 localhost 部署

### 可以視需求展開

- [ ] 支援 Android app
- [ ] 結合 Git
    - Git 整合延伸成 work item 與 code traceability
- [ ] 抽出共用 execution / pipeline 契約層
    - 讓 command / query 有共用 handler 契約與 pipeline behavior，可承載 observability、validation、authorization 等 cross-cutting concern
    - 這一層不預設綁定 DDD，未來需求更成熟時再決定要走 `IExecutor`、`ICommandHandler` / `IQueryHandler`，或其他更合適的抽象
- [ ] 用 transaction script 重寫一份
- [ ] 事件驅動整合
- [ ] 離線優先 / sync
- [ ] feature flag / trunk-based development
