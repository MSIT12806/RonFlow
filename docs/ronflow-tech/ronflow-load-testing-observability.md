# RonFlow 的壓力測試與 observability 實踐

## 為什麼這篇文章值得寫
RonFlow 已經把「壓力測試 & observability」列進自己的技術特點，但如果只看到 `k6`、`Performance` 環境、`Server-Timing` 這些關鍵字，還是很容易停在工具層的理解。

真正重要的是三件事：
- RonFlow 想回答的是什麼效能問題
- 壓力測試與 observability 要怎麼一起配合
- 在加上這些觀測能力時，怎麼避免把 controller / application / infrastructure 本體弄髒，破壞 SRP

這篇文章就是把這三件事說清楚。

## 這個技術概念是什麼
RonFlow 現在做的不是 generic 壓測展示，而是一套很明確的效能診斷流程：
- 先準備獨立的 `Performance` 環境與資料庫
- 用真實 HTTP API seed 出可重複的資料集
- 用 `k6` 對真實 board read scenario 施加固定負載
- 再把 server-side 的分層耗時帶回壓測結果裡看

也就是說，RonFlow 的目標不是只知道「慢不慢」，而是要知道：
- 是整體 request 變慢
- 還是 application / store 內部某一層真的慢
- 還是主機排隊、資源競爭、process 外圍成本被放大

## RonFlow 現在如何做壓力測試
### 1. 用獨立的 Performance 環境隔離資料
RonFlow 與 RonAuth 都有自己的 `Performance` 啟動設定與 SQLite 資料庫。

這樣做的目的不是模擬 production，而是保證：
- 壓測資料不污染平常開發資料
- 每次壓測都可以 reset 到乾淨狀態
- baseline 與優化後結果可以在同一套前提下重跑

目前相關入口包括：
- `scripts/performance/Reset-PerformanceDatabases.ps1`
- `scripts/performance/Seed-PerformanceData.ps1`
- `scripts/performance/Run-BoardReadLoadTest.ps1`
- `scripts/performance/Run-BoardReadLoadTest-Workflow.ps1`

### 2. 用真實 API seed 資料，而不是直接改 SQLite 檔
RonFlow 的 performance seed 不走「直接寫 DB」這條路，而是透過真實 API：
- 註冊 / 登入使用者
- 建立 project
- 建立 task
- 視需要建立 members / invitations

這個做法的價值是：
- 壓測資料更接近實際業務流程產生的 shape
- seed 過程本身也順便驗證 supporting domain 邊界仍然成立

### 3. 先把 baseline 建起來，再談優化
RonFlow 目前把 board read 當第一個壓測場景，並先建立：
- `S @ 20 VUs / 1m` 作為日常 regression gate
- `M @ 20 VUs / 1m` 作為資料量敏感度檢查

這代表 RonFlow 不是先憑感覺改查詢，而是先固定條件量出：
- 小資料量下是否穩定
- 資料量上升後是否明顯退化

## RonFlow 現在如何做 observability
### 1. 不只看 client 端總耗時，也看 server-side 分層耗時
壓測一開始只有 `ronflow_board_duration`，也就是從 `k6` 看出去的整體 request duration。

這種數字很重要，但它只能告訴你「request 很慢」，沒辦法回答「慢在 controller、application、store，還是慢在外圍排隊」。

因此 RonFlow 現在進一步把 board read 的 server-side timing 放進 `Server-Timing` header，然後再由 `k6` 解析回來，形成這些 metrics：
- `ronflow_board_controller_duration`
- `ronflow_board_application_duration`
- `ronflow_board_store_duration`

這樣壓測結果裡就同時有：
- request 整體耗時
- controller 層耗時
- application 層耗時
- store 層耗時

### 2. 目前的觀測訊號是 request-scoped，而不是 full observability platform
RonFlow 現在做的是最小可行 observability，不是一次把整套 metrics / tracing / dashboard 都上齊。

目前這套做法的定位是：
- 先在最有問題的 board read 路徑上放入可重複、可解讀的訊號
- 先回答 bottleneck 在哪一層
- 等問題定位更清楚，再決定是否需要更重的 metrics 或 tracing 基礎建設

## 背後的設計精神
### 1. 壓力測試不是表演，而是建立可比較的基準
RonFlow 做 baseline 的目的，不是對外宣稱「系統可以扛多少人」，而是為了建立可重複比較的起跑線。

所以它更重視的是：
- 條件固定
- 資料可重建
- 結果可比較

### 2. observability 是 cross-cutting concern，不應長期混進 use case 本體
如果把 stopwatch、log threshold、header formatting 全部直接寫進 controller 與 application service，本來純粹的業務方法就會多出新的 change reason：
- 業務流程改了要改
- 觀測格式改了也要改
- timing 輸出策略改了也要改

這會讓方法逐漸偏離 SRP。

RonFlow 這次重構的核心，就是把這種 cross-cutting concern 從本體搬出去。

## RonFlow 怎麼保持 SRP
### 1. Controller 只做 HTTP orchestration，不自己量測
`ProjectsController.GetBoard` 現在只負責：
- 取 current user
- 呼叫 query service
- 更新 project presence
- 把結果轉成 HTTP response

它不再自己：
- 建 stopwatch
- 寫 log threshold
- 拼 `Server-Timing` header

這些都交給 API filter。

### 2. Application service 回到純 use case 邏輯
`GetProjectBoardQueryService` 現在重新回到最單純的責任：
- access check
- read store
- mapping read model

它不再直接知道：
- `ILogger`
- `Server-Timing`
- slow log threshold

真正的 application timing 改由 `ObservedGetProjectBoardQueryService` decorator 量測。

### 3. Store 本體只管資料讀取，observability 交給 decorator
`SqliteCoreFlowReadStore` 現在只保留：
- 讀 project
- 掃 task
- 組裝 `ProjectBoardModel`

store 的觀測責任改由 `ObservedCoreFlowReadStore` decorator 承擔。

這樣做的好處是：
- 資料讀取流程本身更好懂
- 觀測策略要改時，不用改真正的 SQLite adapter 邏輯

### 4. HTTP response header 屬於 API concern，交給 filter
`Server-Timing` 是 HTTP transport concern，不是 application concern。

因此 RonFlow 把它放在 `BoardReadServerTimingFilter` 這一層處理：
- request 進 action 前 reset timing context
- action 執行後收集 controller / application / store timing
- 最後寫回 `Server-Timing` header

這個責任放在 API filter，比放在 controller action 本體更合理。

## 目前實作長什麼樣子
### Application 層
- `BoardReadObservabilityContext.cs`
- `IGetProjectBoardQueryService.cs`
- `GetProjectBoardQueryService.cs`
- `ObservedGetProjectBoardQueryService.cs`

這裡的結構重點是：
- 介面定義 use case 對外能力
- 純 service 專注在業務邏輯
- decorator 專注在 timing

### Infrastructure 層
- `SqliteCoreFlowReadStore.cs`
- `ObservedCoreFlowReadStore.cs`

這裡的結構重點是：
- 真正的 SQLite store 不負責 observability
- decorator 才負責把 store 的耗時寫進 timing context

### API 層
- `ProjectsController.cs`
- `BoardReadServerTimingFilter.cs`

這裡的結構重點是：
- controller 只做 request orchestration
- filter 才負責寫 `Server-Timing`

### k6
- `scripts/performance/k6/board-read.k6.js`

這支腳本除了量 request duration，也會解析 `Server-Timing` header，把它轉成：
- `ronflow_board_controller_duration`
- `ronflow_board_application_duration`
- `ronflow_board_store_duration`

## 這個做法的優點
### 1. 可以直接在壓測結果裡看分層耗時
不用另外翻 server log，就可以在 `k6` summary 裡看到 server-side layer timing。

### 2. 比把 observability 寫死在 controller / service 本體更乾淨
現在 timing 與 header formatting 都是抽離的 cross-cutting concern。

### 3. 更容易再往下演進
如果未來要改成：
- middleware
- OpenTelemetry
- metrics exporter
- 更通用的 decorator / tracing pipeline

現在這個 shape 比 inline stopwatch 更容易演進。

## 它的限制與下一步
### 1. 目前只覆蓋 board read 這條關鍵路徑
這是一個刻意的選擇，不是平台級 observability 全面完成。

### 2. 目前看到的是 layer-level，不是更細的 query phase
現在的分層已經足夠回答「慢在 application 還是 store」。

如果未來還要更細，例如拆到：
- project lookup
- task scan
- JSON deserialize
- materialize / sort

那就代表需要再抽更細的 collaborator，或引入更下層的資料存取觀測，而不是重新把 inline stopwatch 塞回本體。

### 3. `Server-Timing` 能幫忙分辨內部慢還是外部慢，但還不是完整 root cause
如果未來看到：
- `ronflow_board_duration` 很高
- 但 controller / application / store 都不高

那通常表示 bottleneck 比較可能在：
- thread pool 排隊
- process / host 資源競爭
- SQLite lock / wait
- request 進出應用程式前後的外圍成本

這時候就應該再補 host/process 層級的觀測，而不是只在 use case 裡加更多 stopwatch。

## 總結
RonFlow 的壓力測試與 observability，不只是把 `k6` 跑起來而已。

它真正做的是：
- 用獨立 Performance 環境建立可比較 baseline
- 用真實 API seed 資料
- 用 `Server-Timing + k6` 把 server-side 分層耗時帶回壓測結果
- 再用 filter / decorator 的方式，盡量保持 controller、application、store 本體的 SRP

也就是說，RonFlow 現在的方向不是「哪裡需要觀測就把 log 塞進去」，而是：
- 先找到最值得觀測的路徑
- 再用較乾淨的架構形狀把觀測能力掛上去

這才是這個技術特點真正有價值的地方。