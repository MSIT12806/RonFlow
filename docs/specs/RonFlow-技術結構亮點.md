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
- [x] Task mutation lock policy 與 aggregate execution result
    - lock 規則不再散落在各個 command service 手寫判斷，而是由 domain 中的 `TaskMutationLockPolicy` 定義各 mutation 的 lock requirement
    - application 層的 `TaskMutationGuard` 將 runtime lock 狀態轉成 `TaskMutationAuthorization`
    - `task.UpdateDetails(...)`、`task.ReplaceSubtasks(...)`、`task.ChangeState(...)` 等 aggregate mutation method 直接回傳 execution result，其中包含 `Locked`，讓 lock 成為 mutation contract 的一部分，而不是額外記得補的 cross-check
    - 實踐細節見 [RonFlow 的多人協作 / 即時同步設計](../ronflow-tech/ronflow-real-time-collaboration.md)

## 進行中

- [ ] 讓 AI 可以順利使用 RonFlow（RonFlow v0.3）
    - spec: [RonFlow AI Interaction Surface Spec](./ronflow-ai-interaction-surface-spec.md)
    - 提供 AI bootstrap、領域名詞表、capabilities manifest，讓 AI 能逐步理解系統與可用操作
    - 已補上 AI discovery surface 的 glossary 與 current work summary，讓 AI 能先縮小任務集合，再進入單一 task detail；實踐細節見 [RonFlow 的 AI discovery surface](../ronflow-tech/ronflow-ai-discovery-surface.md)
    - 提供 read-first 的摘要查詢、proposal / preview、approval、audit trail，降低 AI 誤操作風險
    - 結構化 DoD：project subtask templates 與 task checklist review gating

## 下一批值得展現

### 一定要做

- [ ] 加入權限，體現 supporting domain 如何互動
    - authorization / RBAC / policy-based access control
- [ ] 加入報表，實作 projection
    - 結構化 logging、metrics、distributed tracing、健康檢查
- [ ] 資料庫遷移與 schema evolution
- [ ] 資料切片
- [ ] 多租戶 / workspace 邊界
- [ ] html editor / markdown editor
- [ ] 檔案附件 / 物件儲存
- [ ] 部署到正式機的流程，例如 Docker、Health Check 等

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