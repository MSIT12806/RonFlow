## already
- [x] SDD, ATDD, TDD
    - spec-first + acceptance-first 的 Outside-In 開發流程
- [x] clean architecture
    - domain / application / infrastructure 分層
- [x] DDD
- [x] CQRS
- [x] 實現 office system level 通知
    - Web Push + Service Worker 整合
- [x] background service

### what?
這是 AI 認為是特點，但是我不知道裡面實作細節的東西。

- [x] workflow state 與 lifecycle state 雙狀態軸設計
- [x] 活動紀錄 / audit trail 雛形

## wish

### 一定要做
- [ ] 加入權限，體現 supporting domain 如何互動
    - authorization / RBAC / policy-based access control
- [ ] 加入報表，實作 projection
- [ ] 加入多人協作的設計
    - 多人協作下的 concurrency control、即時協作
- [ ] observability
    - 結構化 logging、metrics、distributed tracing、健康檢查。
- [ ] 資料庫遷移與 schema evolution
- [ ] 多租戶 / workspace 邊界
- [ ] html editor / markdown editor
- [ ] 檔案附件 / 物件儲存

### maybe
- [ ] 支援 android app
- [ ] 結合 git
    - Git 整合延伸成 work item 與 code traceability
- [ ] 用 transaction script 重寫一份
- [ ] 事件驅動整合
- [ ] 離線優先 / sync
- [ ] feature flag / trunk-based development