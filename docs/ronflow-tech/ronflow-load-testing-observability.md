# RonFlow 的壓力測試與 observability 實踐

## 為什麼這篇文章值得寫
RonFlow 已經把「壓力測試 & observability」列進自己的技術特點，但如果只看到 `k6`、`Performance` 環境、`Server-Timing` 這些關鍵字，還是很容易停在工具層的理解。

真正重要的是三件事：
- RonFlow 想回答的是什麼效能問題
- 壓力測試與 observability 要怎麼一起配合
- 在加上這些觀測能力時，怎麼避免把 controller / application / infrastructure 本體弄髒，破壞 SRP，甚至避免讓 observability 本身留在 application 專案裡

這篇文章就是把這三件事說清楚。

如果你想先看這些做法在 pattern language 裡該怎麼命名，例如 adapter、decorator、filter、event handler、orchestrator 的分工，可以先讀：
- [RonFlow 的 cross-cutting pattern language](./ronflow-cross-cutting-pattern-language.md)

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

如果換成 pattern language 來說，RonFlow 現在這套做法不是單一 pattern，而是一組 pattern 的組合：
- `ProjectsController` 是 HTTP adapter
- `[ObservedOperation(...)]` 是啟用觀測的 metadata anchor
- `ObservedOperationTimingMiddleware` 是 request outer ring 上的 middleware
- `ObservedOperationServerTimingFilter` 是 HTTP boundary 上的 filter
- `ObservedGetProjectBoardQueryService` 是 use case decorator
- `ObservedCoreFlowReadStore` 是 read store decorator
- `Program.cs` 是 composition root，負責決定整個包裝順序

也就是說，RonFlow 的壓測與 observability 不是「在原本方法裡多寫幾個 stopwatch」，而是把觀測能力掛在系統邊界與 port 外圍。

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

這種數字很重要，但它只能告訴你「request 很慢」，沒辦法回答「慢在 controller、application、store，還是慢在 request 外圍」。

因此 RonFlow 現在進一步把 board read 的 server-side timing 放進 `Server-Timing` header，然後再由 `k6` 解析回來，形成這些 metrics：
- `ronflow_board_response_start_duration`
- `ronflow_board_current_user_sync_duration`
- `ronflow_board_active_session_duration`
- `ronflow_board_controller_duration`
- `ronflow_board_application_duration`
- `ronflow_board_store_duration`

這樣壓測結果裡就同時有：
- request 整體耗時
- request outer ring 到 response-start 的耗時
- current-user-sync 這類 middleware slice 的耗時
- controller 層耗時
- application 層耗時
- store 層耗時

這一層資訊很重要，因為它把「慢在 query path」和「慢在 outer ring」分開了。這次 `M @ 20 VUs / 1m` 的結果就是一個很明顯的例子：
- `ronflow_board_duration p95 = 2332.30 ms`
- `ronflow_board_response_start_duration p95 = 2329.06 ms`
- `ronflow_board_current_user_sync_duration p95 = 2268.95 ms`
- `ronflow_board_controller / application / store p95` 都只有大約 `76 ms`

這代表目前最可疑的 bottleneck 已經不是 board query 本身，而是進入 controller 之前的 `CurrentUserDirectorySyncMiddleware` 這一圈。

### 2. 目前的觀測訊號是 request-scoped，而不是 full observability platform
RonFlow 現在做的是最小可行 observability，不是一次把整套 metrics / tracing / dashboard 都上齊。

目前這套做法的定位是：
- 先在最有問題的 board read 路徑上放入可重複、可解讀的訊號
- 先回答 bottleneck 在哪一層
- 等問題定位更清楚，再決定是否需要更重的 metrics 或 tracing 基礎建設

如果用更 pattern language 的方式說，這代表 RonFlow 現在優先做的是：
- metadata-driven request observation
- request outer ring middleware
- request-scoped filter
- use case decorator
- read store decorator

而不是一開始就做完整的 metrics platform 或 distributed tracing platform。

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

再更進一步，如果連 decorator、timing context 都還留在 application project 裡，語意上仍然有一點「觀測機制住在 use case 所在層」的味道。

因此 RonFlow 現在採用的進一步整理方式是：
- use case 介面留在 application
- observability 機制抽到獨立的 `RonFlow.Observability` project
- 由最外層 composition root 決定要不要把純 service 包上 observability decorator

這樣做的意思不是多加一個新的 business layer，而是把 observability 視為獨立的 cross-cutting module / outer ring concern。

這一點很重要，因為如果只說「我們用了 adapter」，團隊其實很容易繼續把很多不同性質的東西都往同一個詞裡塞。RonFlow 這次比較精確的說法應該是：
- adapter 負責邊界轉接
- decorator 負責包住既有 port 做 cross-cutting timing
- filter 負責 HTTP response enrichment
- composition root 負責組裝順序

### 3. 為了讓 application / store 可被探測，RonFlow 做了哪些重構
RonFlow 這次不是先把 stopwatch 塞進 `GetProjectBoardQueryService` 和 `SqliteCoreFlowReadStore`，而是先把 board read 這條路徑整理成「可被外部包裝、可被統一收集 timing」的形狀。

原本真正卡住 observability 的點，不是因為 board read 不能量，而是因為如果只站在 HTTP 外面看，拿得到的只有整體 request duration。這種數字只能回答「慢」，不能回答：
- 慢在 controller、application、store 的哪一層
- 還是慢在 request outer ring

如果沒有額外重構，想再往裡面看，最直接的方法通常只剩：
- 把 stopwatch 直接寫進 controller action
- 把 stopwatch 直接寫進 application service method body
- 把 stopwatch 直接寫進 SQLite store

這種做法雖然量得到，但也代表 observability 會直接混進業務本體，讓 application / infrastructure 多出新的 change reason。

RonFlow 後來做的重構，重點有三個：

第一個，是把 application 節點固定成可被 decorator 包裹的 port。`GetProjectBoardQueryService` 仍然保留純 use case 邏輯，但由 composition root 決定 controller 實際拿到的是 `IGetProjectBoardQueryService`，而不是把量測碼直接寫回 service 本體。這樣一來，`ObservedGetProjectBoardQueryService` 就可以在不污染 application project 的前提下，包住 use case 並記錄 application timing。

第二個，是對 read store 做同樣的整理。`GetProjectBoardQueryService` 透過 `ICoreFlowReadStore` 讀資料，因此 `ObservedCoreFlowReadStore` 可以從外部包住真正的 store，專門量測 store timing。這一步很重要，因為 application timing 和 store timing 只有在這兩層都有穩定包裝點時，才真的拆得開；否則看到的只會是 application 把 store 一起包進去的總時間。

第三個，是引入 request-scoped 的 timing context，讓不同節點量到的時間可以被串成同一筆 request 訊號。`ObservedOperationTimingContext` 會在 request 開始時建立，controller filter、application decorator、store decorator、middleware slice 都把自己的 timing 回填到這個 context，最後再由 `ObservedOperationTimingMiddleware` 統一輸出成 `Server-Timing` header。

也因為有了這個 context，RonFlow 才不需要在每一層各自決定要怎麼寫 header 或怎麼輸出 log。各層只需要量自己的時間，至於最後怎麼彙整、怎麼輸出，是外圍 observability infrastructure 的責任。

如果再濃縮一次，這次重構的本質不是「讓 application 內建 observability」，而是：
- 讓 controller / application / store 都有可被外部包裹的穩定邊界
- 讓 request 期間不同節點的 timing 有共同的暫存位置
- 讓 timing 的彙整與輸出留在 observability project，而不是回流到業務方法裡

也正因為先做了這些重構，RonFlow 後來才有辦法在不破壞 SRP 的前提下，把 controller、application、store、outer ring middleware 的耗時一起帶回 `k6` 報表。

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

真正的 application timing 改由 `RonFlow.Observability` project 裡的 `ObservedGetProjectBoardQueryService` decorator 量測。

也就是說，RonFlow 不只是把 timing 從 method body 拿掉，而是連 observability decorator 本身都不再放在 application assembly 裡。

如果未來還有更多 technical concern，例如 transaction、retry、metrics、tracing，這些也比較適合走同一類 pattern：
- 盡量包在 use case port 外層
- 由 composition root 決定順序
- 不讓外圍元件彼此直接形成 hard-coded 依賴

### 3. Store 本體只管資料讀取，observability 交給 decorator
`SqliteCoreFlowReadStore` 現在只保留：
- 讀 project
- 掃 task
- 組裝 `ProjectBoardModel`

store 的觀測責任改由 `RonFlow.Observability` project 裡的 `ObservedCoreFlowReadStore` decorator 承擔。

這樣做的好處是：
- 資料讀取流程本身更好懂
- 觀測策略要改時，不用改真正的 SQLite adapter 邏輯

### 4. HTTP response header 屬於 API concern，交給 filter
`Server-Timing` 是 HTTP transport concern，不是 application concern。

因此 RonFlow 現在把它拆成 generic 的 request observation 元件來處理：
- `ObservedOperationTimingMiddleware` 在 request outer ring 建立 timing context，並補上 `response-start`
- `ObservedOperationServerTimingFilter` 收集 controller timing
- `ObservedOperationResultTimingFilter` 收集 result execution timing
- `CurrentUserDirectorySyncMiddleware` 與 `RonFlowActiveSessionMiddleware` 只在有 active observed operation 時回寫自己的 middleware slice
- 最後統一寫回 `Server-Timing` header

這個責任放在 API filter，比放在 controller action 本體更合理。

更精確地說，RonFlow 現在的做法是：
- API project 提供 composition root 與 HTTP endpoint
- observability project 提供 filter、decorator、timing context
- application / infrastructure project 提供純業務與純資料存取邏輯

這個分工比「把 observability decorator 留在 application project」更符合分層語意。

也因此，RonFlow 這裡的重點不是「多包一層就好了」，而是「要把不同責任對應到對的 pattern 上」：
- HTTP concerns 對應 filter
- use case cross-cutting concerns 對應 decorator
- 系統組裝順序對應 composition root

## 目前實作長什麼樣子
### Application 層
- `IGetProjectBoardQueryService.cs`
- `GetProjectBoardQueryService.cs`

這裡的結構重點是：
- 介面定義 use case 對外能力
- 純 service 專注在業務邏輯

### Infrastructure 層
- `SqliteCoreFlowReadStore.cs`

這裡的結構重點是：
- 真正的 SQLite store 不負責 observability

### Observability 層
- `RonFlow.Observability.csproj`
- `ObservedOperationAttribute.cs`
- `ObservedOperationNames.cs`
- `ObservedOperationTimingContext.cs`
- `ObservedOperationTimingMiddleware.cs`
- `ObservedOperationServerTimingFilter.cs`
- `ObservedOperationResultTimingFilter.cs`
- `ObservedGetProjectBoardQueryService.cs`
- `ObservedCoreFlowReadStore.cs`
- `RonFlowObservabilityMetrics.cs`

這裡的結構重點是：
- observability 不是 business layer，而是獨立的 cross-cutting project
- decorator、metadata、timing context、request timing middleware 都住在這裡，而不是住在 application / infrastructure project
- API-specific 的 `Server-Timing` filter 也放在這裡，由 API composition root 掛上去

如果未來 RonFlow 還要加入更多 cross-cutting concern，這個 project 也提供了一個很自然的放置位置。但不是所有東西都應該放進來，例如 email 通知、外部系統同步這類「用例完成後的副作用」，很多時候更像 event handler concern，而不是 decorator concern。這一點在 [RonFlow 的 cross-cutting pattern language](./ronflow-cross-cutting-pattern-language.md) 有更完整的說明。

### API 層
- `ProjectsController.cs`
- `CurrentUserDirectorySyncMiddleware.cs`
- `RonFlowActiveSessionMiddleware.cs`

這裡的結構重點是：
- controller 只做 request orchestration
- `ProjectsController.GetBoard` 只用 `[ObservedOperation(ObservedOperationNames.BoardRead)]` 宣告這條 route 需要進入觀測
- 真正的 observability 元件由 composition root 從 `RonFlow.Observability` 接進來

### k6
- `scripts/performance/k6/board-read.k6.js`

這支腳本除了量 request duration，也會解析 generic `Server-Timing` key，再把它轉成 board read 報表需要的 metrics：
- `response-start` -> `ronflow_board_response_start_duration`
- `middleware-current-user-sync` -> `ronflow_board_current_user_sync_duration`
- `middleware-active-session` -> `ronflow_board_active_session_duration`
- `controller` -> `ronflow_board_controller_duration`
- `application` -> `ronflow_board_application_duration`
- `store` -> `ronflow_board_store_duration`

也就是說，app 端 observability 已經泛用化，但 load test script 仍然可以保留 scenario-specific 的命名與 threshold：
- `ronflow_board_controller_duration`
- `ronflow_board_application_duration`
- `ronflow_board_store_duration`

## 這個做法的優點
### 1. 可以直接在壓測結果裡看分層與 outer-ring 耗時
不用另外翻 server log，就可以在 `k6` summary 裡同時看到：
- server-side layer timing
- response-start 這類 outer-ring timing
- middleware slice timing

也因此，這次 M-scale 報表才能直接把問題收斂到 `Current-user-sync`，而不是讓人繼續在 board query phase 上盲猜。

### 2. 比把 observability 寫死在 controller / service 本體更乾淨
現在 timing 與 header formatting 都是抽離的 cross-cutting concern。

而且不只是「從 method body 抽出來」而已，還進一步做到：
- observability 不住在 application project
- store 的觀測 decorator 也不住在 infrastructure project

這讓 application / infrastructure 的語意更乾淨。

### 3. 更容易再往下演進
如果未來要改成：
- middleware
- OpenTelemetry
- metrics exporter
- 更通用的 decorator / tracing pipeline

現在這個 shape 比 inline stopwatch 更容易演進。

### 4. 分層語意更一致
如果把 observability 繼續留在 application assembly，雖然比 inline stopwatch 好，但還是容易產生「application project 同時住著業務能力與診斷能力」的混合感。

把它抽成獨立 project 之後，會更清楚地表達：
- application 是用例與業務協調
- infrastructure 是技術性資料存取
- observability 是外圍診斷能力

這也是為什麼 RonFlow 現在開始用比較明確的 pattern language 來描述這件事：如果沒有這些詞彙，設計討論很容易退化成「全部都是 adapter」，但實際上 controller、filter、decorator、event handler、orchestrator 根本不是同一種責任。

## 它的限制與下一步
### 1. 目前只覆蓋 board read 這條關鍵路徑
這是一個刻意的選擇，不是平台級 observability 全面完成。

### 2. 目前看到的已經不只 layer-level，但還不是更細的 query phase
現在的訊號已經足夠回答兩種不同問題：
- 慢在 request outer ring，還是慢在 controller / application / store
- outer ring 裡是不是某個 middleware slice 特別突出

這也是為什麼這次 M-scale 報表能直接把注意力收斂到 `Current-user-sync`，而不是繼續懷疑 board read query 本身。

如果未來還要更細，例如拆到：
- project lookup
- task scan
- JSON deserialize
- materialize / sort

那就代表需要再抽更細的 collaborator，或引入更下層的資料存取觀測，而不是重新把 inline stopwatch 塞回本體。

### 3. `Server-Timing` 能幫忙分辨內部慢還是外部慢，但還不是完整 root cause
如果未來看到：
- `ronflow_board_duration` 很高
- `ronflow_board_response_start_duration` 也很高
- 但 controller / application / store 都不高

那通常表示 bottleneck 比較可能在：
- thread pool 排隊
- process / host 資源競爭
- SQLite lock / wait
- request 進出應用程式前後的外圍成本

而如果像這次一樣，`ronflow_board_current_user_sync_duration` 自己就已經明顯貼近總耗時，那下一步就不該先去拆 board query phase，而是要先往 `CurrentUserDirectorySyncMiddleware` 內部看：
- directory lookup / upsert 是否有不必要的 I/O
- 是否每次 board read 都做了可避免的同步
- 在併發下是否放大了 SQLite lock / wait
- 是否可以把部分同步改成更延後或更快取的策略

這時候就應該再補 host/process 層級的觀測，而不是只在 use case 裡加更多 stopwatch。

### 4. 獨立 project 不代表從此不需要取捨
把 observability 抽成獨立 project，確實會讓分層語意更乾淨，但也會帶來新的組裝成本：
- 需要多一個 project reference
- composition root 要多接一層 decorator / filter
- 測試時要注意 interface 與 decorator 的 wiring

所以這不是「永遠越多 project 越好」，而是當觀測能力已經成為長期存在的架構要素時，這樣拆分才值得。

## 總結
RonFlow 的壓力測試與 observability，不只是把 `k6` 跑起來而已。

它真正做的是：
- 用獨立 Performance 環境建立可比較 baseline
- 用真實 API seed 資料
- 用 `Server-Timing + k6` 把 server-side 分層耗時與 outer-ring 耗時帶回壓測結果
- 再用獨立 `RonFlow.Observability` project + metadata / middleware / filter / decorator 的方式，盡量保持 controller、application、store 本體的 SRP 與分層語意

也就是說，RonFlow 現在的方向不是「哪裡需要觀測就把 log 塞進去」，而是：
- 先找到最值得觀測的路徑
- 再用較乾淨的架構形狀把觀測能力掛上去
- 並且把 observability 視為獨立的 cross-cutting concern，而不是繼續留在 application project 裡

如果再用 pattern language 濃縮一次，RonFlow 現在採用的是：
- adapter 承接邊界
- metadata anchor 決定哪些 endpoint 進入觀測
- middleware 量 request outer ring
- decorator 包住 use case 與 read store
- filter 補強 HTTP response
- composition root 控制 wiring

這才是這個技術特點真正有價值的地方。