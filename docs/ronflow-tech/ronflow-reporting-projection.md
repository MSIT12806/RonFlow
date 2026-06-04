# RonFlow 的 reporting / projection 實踐

## 為什麼這篇文章值得寫
RonFlow 這次的 reporting 功能，不只是多一個查詢 API 或多一個畫面 tab，而是第一次把 command side 的狀態變化，刻意整理成可以被背景流程消費的 projection source。

這件事值得被單獨寫成文章，因為它剛好落在一個重要的工程節點：
- 系統已經不再只有同步 request-response 的互動
- read model 開始有自己的資料形狀與更新節奏
- command side 與 reporting side 的責任被明確切開
- 團隊可以先用單機 outbox + consumer 驗證整條鏈，再決定未來是否要推進到真正的 message queue

也就是說，RonFlow 不是直接跳到很重的分散式事件架構，而是先把 projection 這條路打通。

## 這個技術概念是什麼
Reporting / projection 的核心，不是單純把資料「查出來」，而是把系統中已經發生的業務變化，轉成適合查詢與統計的 read model。

如果每次都直接從 write model 現算報表，常會遇到幾個問題：
- 查詢需要的 shape 不一定適合直接從 aggregate 組裝
- 同一份統計可能需要不同粒度，例如日 / 週
- 報表需求容易反向污染 command side 的 domain model

因此常見做法會是：
- command side 負責真正的狀態改變
- 某些重要變化被轉成 event 或 event-like source
- outbox 暫存待處理訊息
- consumer 在背景把它們套用到 projection store
- query side 只讀 projection 結果

RonFlow 這次第一批報表，就是用這條思路做 `Workflow Throughput`。

## 它背後的設計精神
### 1. 報表不是 command side 的附屬輸出
建立任務、改變 workflow state、完成 task、重新開啟 task，這些是 command side 真正關心的事情；工作流量統計則是讀側關心的事情。

RonFlow 的做法，是讓 command side 在狀態變化發生時，只額外送出 projection source，而不是直接在 command service 裡手刻統計報表。

### 2. 先把非同步讀側打通，再決定要不要上 MQ
這一版沒有直接引入外部 message broker。

RonFlow 目前先用：
- application 層 outbox interface
- in-memory / SQLite outbox implementation
- hosted background service 定期消費

也就是說，先把 eventual consistency 的讀側更新鏈做出來，再決定未來是否需要 RabbitMQ、Azure Service Bus 或其他 broker。這樣的好處是，團隊可以先驗證事件邊界、projection shape、idempotency 與使用價值，而不是一開始就把複雜度拉滿。

### 3. Query side 應該對 UI 友善，而不是對 aggregate 友善
前端的 Project Reports Page 不需要知道 task aggregate 內部長什麼樣，它只需要拿到：
- projectId
- bucketType
- lastUpdatedAt
- 每個 bucket 的 created / movedToActive / movedToReview / completed / reopened 統計

這也是 projection 的價值：read model 可以直接長成 UI 需要的形狀。

## 這樣做的優點
### 1. Command side 與 reporting side 邊界更清楚
建立任務與改變狀態，仍然專注在原本的業務規則；報表累積則交給 projection store 處理。

### 2. 報表查詢可以有自己的最佳化方向
RonFlow 的 `WorkflowThroughputBuckets` 直接按 project、bucketType、bucketStart 累積統計，不需要每次 query 再回頭掃所有 task activity。

### 3. 已經踩進 event-driven thinking，但還沒被 broker 綁死
現在的 `WorkflowThroughputProjectionSource` 已經是 event-like message。未來若真的要接外部 MQ，會比從同步 CRUD 直接硬拆容易很多。

### 4. 前後端可以以較弱一致性協作
這次前端報表頁明確接受「投影可能有短暫延遲」，並用 loading / empty / last updated 等資訊避免誤導使用者把 projection 當成主交易的同步結果。

## 代價與限制
### 1. 系統開始需要面對 eventual consistency
主交易完成，不代表 projection 已經同步更新。這需要 UI、測試與工程心智一起承接。

### 2. idempotency 目前仍是第一批版本
目前 outbox message 會被標記 processed，projection bucket 也用固定 counter update。這足以支撐現在的單機流程，但若未來有重播、併發 consumer、跨程序重試，還需要更明確的去重策略。

### 3. 報表範圍目前只落地第一張
現在真正完整打通的是 `Workflow Throughput`。`Task Aging` 與 `Cycle Time / Lead Time` 已先進 spec 與 UI 入口，但仍是後續延伸。

## RonFlow 裡是怎麼實作的
### 1. Command side 產生 projection source
這次的 source 來自兩個既有 command service：
- `CreateTaskCommandService.Create`
- `ChangeTaskStateCommandService.Change`

建立 task 時，系統會 enqueue `TaskCreated`；改變 workflow state 時，系統會 enqueue：
- `TaskStateChanged`
- 必要時再補 `TaskCompleted`
- 必要時再補 `TaskReopened`

這代表 projection source 並不是 controller 自己推，也不是前端補送，而是直接從 command side 的正式狀態改變產生。

### 2. Application 層定義 outbox / store contract
`WorkflowThroughputProjectionModels.cs` 定義了這次 reporting slice 的主要 application contract：
- `IWorkflowThroughputProjectionOutbox`
- `IWorkflowThroughputProjectionStore`
- `ProcessWorkflowThroughputProjectionService`
- `GetWorkflowThroughputReportQueryService`

這裡最重要的地方是，RonFlow 沒有把 projection 直接綁死在某個資料庫實作，而是先把寫入 source、套用 projection、查詢報表這三個責任切開。

### 3. Infrastructure 同時提供 in-memory 與 SQLite 落地
`WorkflowThroughputProjectionInfrastructure.cs` 目前有兩組實作：
- `InMemoryWorkflowThroughputProjectionOutbox` / `InMemoryWorkflowThroughputProjectionStore`
- `SqliteWorkflowThroughputProjectionOutbox` / `SqliteWorkflowThroughputProjectionStore`

這讓測試環境可以快速用 in-memory 驗證整條流程，而非測試環境則可把 outbox 與 projection bucket 真正落到 SQLite。

SQLite 端新增了兩張 table：
- `WorkflowThroughputOutbox`
- `WorkflowThroughputBuckets`

前者存待處理 message，後者存日 / 週 bucket 統計。

### 4. Background consumer 定期處理 pending message
`WorkflowThroughputProjectionBackgroundService` 每 5 秒喚醒一次，呼叫 `ProcessWorkflowThroughputProjectionService.ProcessPending()`。

這條 consumer 目前是 polling-based background service，不是外部 queue consumer，但對使用者與前端來說，它已經是一條正式的非同步 projection update 路徑。

### 5. Query side 透過專屬 reports controller 暴露
這次新增的 API 入口是：
- `GET /api/projects/{projectId}/reports/workflow-throughput?bucket=day|week`

`ProjectReportsController` 會先做：
- authenticated user 判斷
- bucket 參數驗證
- project access boundary 檢查

接著交給 `GetWorkflowThroughputReportQueryService` 從 projection store 讀出 `WorkflowThroughputReportView`。

### 6. Frontend 以 Project Reports View 承接第一批報表
這次前端新增的是 project-level 的 `ProjectReportsView`：
- 從 Project Board 點擊 `報表` 入口進入
- 預設顯示 `工作流量`
- 可切換 `day / week`
- `Task Aging` 與 `Cycle Time / Lead Time` 先提供佔位與明確交付順序

另外，前端還補了一個很重要的細節：reports view 被納入 workspace polling。這是因為 projection 是背景更新，如果 reports page 不 refresh，就會讓使用者看到停在舊投影的畫面。

## 測試是怎麼承接的
這條 reporting/projection slice 目前至少有三層驗證：

### API integration
`ReportingProjectionApiIntegrationTests` 驗證：
- task 建立與完成後，daily bucket 會正確累積
- task reopen 後，reopened counter 會遞增
- weekly bucket 會把同週事件聚合起來

### Frontend acceptance
前端新增了兩支 E2E：
- `project-reports.screen.acceptance.spec.ts`
- `project-reports.behavior.acceptance.spec.ts`

前者驗證從 Project Board 進入報表頁的 user-facing flow；後者驗證 task workflow 流轉後，工作流量報表真的會讀出正確統計。

### Localhost deploy verification
這次也實際把變更部署到 localhost，確認：
- `http://localhost/` 前端可達
- `http://localhost/ronflow-api/api/projects` 在未登入時會回 401
- `http://localhost/ronauth-api/api/auth/login` / `register` 路由存在

這一步很重要，因為 reporting / projection 是跨前端、後端、背景 service 與 IIS 部署路徑的功能，不只是測試過就算完成。

## 這件事對 RonFlow 代表什麼
這次的 reporting / projection 實踐，讓 RonFlow 從「有 command 與 query 的 CQRS 系統」再往前走一步，變成「讀側可以接受較弱一致性、並由背景 consumer 維護 projection」的系統。

這不只是在做一張報表，而是在替未來這些方向鋪路：
- Task Aging projection
- Cycle Time / Lead Time projection
- AI audit 統計 projection
- 更正式的 MQ / event-driven integration

## 總結
RonFlow 這次採取的不是教科書式的 heavyweight event architecture，而是一條更務實的路：先從真正有產品價值的 `Workflow Throughput` 開始，把 command side、outbox、background consumer、projection store、query API、frontend reports page 一次串起來。

這條路最有價值的地方，不是用了多少名詞，而是它已經證明 RonFlow 可以在不破壞既有核心流程的前提下，引入一條可擴充的 reporting / projection 更新管線。未來如果真的要再往 MQ 或跨 bounded context integration 演進，這次的第一批落地會是非常好的起點。