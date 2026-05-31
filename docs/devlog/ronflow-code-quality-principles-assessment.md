# RonFlow 程式碼品質大雜燴評鑑

## 1. 為什麼要寫這篇

前幾天看到一段很像架構委員會結案報告的文字：SOLID 全數達標、高內聚低耦合、DRY 到一行不多、KISS 到一字不少、零技術債、SonarQube A 評、循環複雜度低於 3。它看起來很像在稱讚程式碼，實際上也很像在諷刺我們有時候會把所有工程美德一股腦貼在專案身上，好像貼滿之後系統就自然健康。

但這段話也適合拿來當 RonFlow 的一面鏡子。

不是為了證明 RonFlow 多完美，而是反過來問：如果把這些原則全部攤開來看，哪些真的能從程式碼裡找到證據？哪些只是局部成立？哪些目前根本不能宣稱？

這篇文章是一次程式碼品質自評。它不是正式 audit，也不是工具掃描報告，而是以目前 RonFlow 的前端與後端程式碼為基礎，做一次偏工程判斷的盤點。

## 2. 評估範圍與方法

本次主要閱讀與比對的範圍包含：

- 前端：Vue / TypeScript 專案，包含 [App.vue](../../code-frontend/src/App.vue)、[useRonFlowBoard.ts](../../code-frontend/src/composables/useRonFlowBoard.ts)、[request.ts](../../code-frontend/src/api/request.ts)、[TaskCommandService.ts](../../code-frontend/src/application/TaskCommandService.ts)、[useApiResource.ts](../../code-frontend/src/composables/useApiResource.ts)、E2E 與 component tests。
- 後端：.NET 專案，包含 [Task.cs](../../code-backend/RonFlow.Domain/Task.cs)、[GetProjectBoardQueryService.cs](../../code-backend/RonFlow.Application/GetProjectBoardQueryService.cs)、[CommandResults.cs](../../code-backend/RonFlow.Application/CommandResults.cs)、[Program.cs](../../code-backend/RonFlow.Api/Program.cs)、Infrastructure、Observability 與 API integration tests。
- 文件：既有技術文章，例如 [RonFlow 的 Clean Architecture 實踐](../ronflow-tech/ronflow-clean-architecture.md)、[RonFlow 的 DDD 實踐](../ronflow-tech/ronflow-ddd.md)、[RonFlow 的 CQRS 實踐](../ronflow-tech/ronflow-cqrs.md)、[RonFlow 的壓力測試與 observability 實踐](../ronflow-tech/ronflow-load-testing-observability.md)。

我沒有在這次評估中重新跑完整 coverage、lint、CI、SonarQube 或複雜度掃描。因此凡是需要工具數據才能成立的主張，都只能標成「未驗證」，不能偷渡成結論。

## 3. 一句話結論

RonFlow 後端的結構品質明顯比一般練習專案成熟，尤其是分層、依賴方向、DDD 語言、CQRS 雛形、API integration tests 與可觀測性拆分，都有具體程式碼支撐；前端也已經有 API boundary、application service、composable 與 acceptance tests 的雛形，但主畫面與 board composable 偏厚，狀態與 UI orchestration 仍有集中化風險。

如果要用比較節制的話說：

```text
RonFlow 不是零技術債的完美範本，但它已經有一個能承接演進的工程骨架。
```

## 4. 前端評鑑

### 4.1 做得好的地方

前端最清楚的優點，是它沒有把所有 HTTP 細節散落在 component 裡。[request.ts](../../code-frontend/src/api/request.ts) 集中處理 API base URL、Bearer token、RonFlow session header、validation error、404、401 session invalidation 等行為。這讓 API 邊界相對明確，component 不需要知道太多 transport detail。

第二個優點是 application service 已經有 command / query 的語意，例如 [TaskCommandService.ts](../../code-frontend/src/application/TaskCommandService.ts) 將 create、change state、update、reorder、subtask、reminder、archive、trash 等操作包成使用案例導向的方法。雖然前端的 CQRS 還不到完整狀態管理架構，但至少已經避免 component 直接操作每一個 endpoint。

第三個優點是 async state 有抽象基礎。[useApiResource.ts](../../code-frontend/src/composables/useApiResource.ts) 把 data、loading、error、execute、reset、setData 包成可重用單元，讓 loading / error state 不必每個畫面重寫一套。這對前端可測性與一致性都有幫助。

第四個優點是前端驗收測試意識很強。`tests/e2e` 底下有多個 screen / behavior acceptance specs，涵蓋 project board、task workflow、task detail、task reminders、task lifecycle、invitation inbox、project members、AI interaction surface、code traceability 等情境。這很符合 RonFlow 一直採用的 spec-first / acceptance-first / Outside-In 開發脈絡。

### 4.2 需要節制看待的地方

[App.vue](../../code-frontend/src/App.vue) 目前大約 653 行，[useRonFlowBoard.ts](../../code-frontend/src/composables/useRonFlowBoard.ts) 大約 886 行。這不代表它們一定錯，但代表前端 orchestration 的重量集中在少數檔案裡。當功能繼續加入，例如 project members、invitations、archived/trash view、code traceability、task lock、push notifications，如果沒有持續拆分 use case 與 view state，這兩個地方會變成修改成本最高的節點。

前端也還不是「高度純函式」或「不可變資料結構優先」的系統。Vue composition API 本來就依賴 `ref`、`computed`、watcher 與事件處理；這是合理的 UI model，不應硬把它包裝成純函式架構。比較務實的說法是：前端有把副作用集中到 API service、composable 與 lifecycle handler，但 UI state 本身仍然是可變狀態。

另外，前端單元 / component tests 目前比 E2E 薄。`src` 底下可見 4 個 `*.spec.ts`，而 E2E spec 有 19 個。這表示使用者流程保護較強，但細部 component、composable、view state 的快速回歸保護仍可增加。

### 4.3 前端小結

前端目前最像一個「以驗收測試與 API contract 驅動的 Vue app」，不是一個已經完全模組化、狀態正規化、觀測完備的前端平台。它的方向是好的，但不能宣稱零技術債，也不適合說每個 component 都已經 SRP 到極致。

## 5. 後端評鑑

### 5.1 做得好的地方

後端最值得肯定的是分層真的有落地，而不是只有資料夾。Domain 放核心模型與規則，例如 [Task.cs](../../code-backend/RonFlow.Domain/Task.cs) 內的 workflow state、lifecycle state、archive、trash、restore、reminder、activity timeline 等行為。Application 放 use case 與 access check，例如 [GetProjectBoardQueryService.cs](../../code-backend/RonFlow.Application/GetProjectBoardQueryService.cs) 將 owner boundary、read store、read model mapping 放在查詢流程中。Infrastructure 則承擔 SQLite / in-memory repository 與 read store。API 作為 HTTP boundary。

這種結構讓依賴方向相對健康：核心模型不需要知道 HTTP、SQLite、OpenTelemetry 或 Vue。外層透過 composition root 組裝內層，符合 [Clean Architecture](../ronflow-tech/ronflow-clean-architecture.md) 的主要精神。

第二個優點是 DDD 語言清楚。Project、Task、WorkflowState、LifecycleState、Archived、Trashed、Reminder、Owner、Invitation、ActivityTimeline 這些詞不是隨便套 pattern，而是對應到 RonFlow 的任務管理語意。尤其 workflow state 與 lifecycle state 分離，能看出系統沒有把所有狀態硬塞成同一條 enum。

第三個優點是 CQRS 採取務實版本。後端有 command service、query service、read store、read model factory，讀寫責任確實有分開。它沒有宣稱自己是分散式 CQRS，也沒有把簡單功能硬拆到失控，這點反而健康。

第四個優點是可觀測性沒有直接污染 use case。RonFlow 把 timing middleware、filter、decorator、metrics 放進 `RonFlow.Observability` project，再由 [Program.cs](../../code-backend/RonFlow.Api/Program.cs) 組裝。這比在 controller、application service、SQLite store 裡到處塞 stopwatch 更符合關注點分離。

第五個優點是後端測試保護相對完整。`RonFlow.Api.Tests` 底下有 API integration tests、domain tests、application tests、測試 factory 與 authenticated user helper。它不是只有 happy path 的 smoke test，而是已經開始測 project owner boundary、invitation、task lifecycle、reminder、push notification、AI interaction surface 等重要流程。

### 5.2 需要節制看待的地方

[Program.cs](../../code-backend/RonFlow.Api/Program.cs) 作為 composition root 合理地集中組裝，但註冊項目已經偏多。它目前仍可接受，因為 composition root 本來就是「把依賴接起來」的地方；但若再加入更多 feature module，後續可能需要模組化註冊，避免 `Program.cs` 變成唯一總開關。

[CommandResults.cs](../../code-backend/RonFlow.Application/CommandResults.cs) 使用多個 nullable payload 與 boolean flag 表示 success、validation、not found、denied、conflict 等狀態。這種寫法顯式、簡單、容易 mapping 到 HTTP response，但隨著 result 種類增加，也會提高錯誤組合的認知負擔。未來若 C# 語言與專案風格允許，可以考慮更接近 discriminated union 的表達方式。

後端不是事件溯源系統。雖然 [Task.cs](../../code-backend/RonFlow.Domain/Task.cs) 有 activity timeline，也有一些 domain-event-like 的紀錄語意，但目前資料持久化不是 event store，不是靠事件重播重建 aggregate。這裡最多只能說「有 audit trail / activity timeline 雛形」，不能說 RonFlow 已完成 [Event Sourcing](../tech-base/event-sourcing.md)。

後端也不是微服務架構。RonFlow 與 RonAuth 是兩個系統邊界，認證與流程管理分離，這是 bounded context / supporting domain 的好跡象；但 RonFlow code-backend 本身仍是一個 modular monolith 風格的 API，不應宣稱微服務邊界已完整成熟。

### 5.3 後端小結

後端目前是 RonFlow 最成熟的工程面。它可以合理宣稱分層清晰、依賴方向健康、DDD / CQRS 採務實落地、可觀測性設計有意識、測試策略有骨架。但不能宣稱事件溯源、零技術債、100% coverage、SonarQube A 評或循環複雜度全低於 3。

## 6. 原則逐項評鑑

| 原則 | 後端 | 前端 | 評語 |
| --- | --- | --- | --- |
| 架構清晰 | 成立度高 | 局部成立 | 後端的 Domain / Application / Infrastructure / API 邊界清楚；前端也有 API / Application / Composable / Component，但主 orchestration 偏集中。 |
| 單一職責 | 成立度高 | 局部成立 | 後端 service 與 observability 拆分良好；前端 component 與 composable 有些地方承擔多種 view orchestration。 |
| 開放封閉 | 局部成立 | 局部成立 | 後端透過 interface、repository、decorator 能新增外圍能力；前端可新增 service/composable，但大型畫面仍可能需要修改中心檔案。 |
| 里氏替換 | 局部成立 | 較不適用 | 後端 repository interface 有 in-memory / SQLite 替換案例；前端較少繼承階層，這條不是主要評估軸。 |
| 介面隔離 | 成立度高 | 局部成立 | 後端 repository/read store/use case interface 相對聚焦；前端 service 邊界存在，但 component 仍直接理解不少 API response shape。 |
| 依賴倒置 | 成立度高 | 局部成立 | 後端 Application 依賴 domain interface，由 API 組裝實作；前端有 service 抽象，但多數仍是直接 module import，不是強 DI。 |
| 高內聚低耦合 | 成立度高 | 局部成立 | 後端按業務與技術責任聚合；前端 task board 相關狀態高度內聚，但也因此形成大 composable。 |
| 分層清晰 | 成立度高 | 局部成立 | 後端分層最明確；前端分層有方向，但 UI orchestration 與 application service 邊界仍可繼續拉清。 |
| 關注點分離 | 成立度高 | 局部成立 | 後端 observability、API、application、domain 分離清楚；前端 HTTP 與 async state 有抽離，但畫面流程仍集中。 |
| 命名達意 | 成立度高 | 成立度高 | Project、Task、WorkflowState、LifecycleState、Archive、Trash、Reminder 等語言一致；前端 command/query/service 命名也清楚。 |
| 顯式優於隱晦 | 成立度高 | 成立度高 | command result、API error、session invalidation、lifecycle state 都偏顯式；代價是類別與結果型別較多。 |
| DRY | 局部成立 | 局部成立 | 有共用 request、useApiResource、base modal、command result pattern；但過度追求 DRY 不是目標，某些重複能保留語意清楚。 |
| KISS | 局部成立 | 局部成立 | 系統沒有為 CQRS / observability 過度分散式化；但 Clean Architecture 與驗收測試本身帶來結構成本。 |
| YAGNI | 局部成立 | 局部成立 | 沒有硬上完整 event sourcing 或微服務是好事；但 AI surface、observability、collaboration 等能力已經超過最小 CRUD，需要持續確認價值。 |
| 最小驚訝原則 | 成立度高 | 局部成立 | 後端命名與層次符合預期；前端使用者流程與 component 命名直觀，但大檔案增加閱讀時的驚訝成本。 |
| 最少知識原則 / 迪米特法則 | 局部成立 | 局部成立 | 後端 controller 多半只找 use case；前端 component 仍知道不少 response 與 view state 細節。 |
| 組合優於繼承 | 成立度高 | 成立度高 | 後端多用 service、decorator、repository 組合；前端 Vue composition API 天然偏組合。 |
| 面向介面程式設計 | 成立度高 | 局部成立 | 後端 interface 與 DI 使用明顯；前端主要靠 module boundary 與 class service，interface programming 較少。 |
| 純函式優先 | 局部成立 | 局部成立 | mapping、factory、value object 有純函式傾向；UI 與 application service 必然含副作用，不應過度宣稱。 |
| 副作用隔離 | 成立度高 | 局部成立 | 後端副作用多在 repository、sender、middleware、background service；前端副作用集中於 API service/composable/lifecycle，但仍與 view orchestration 接近。 |
| 不可變資料結構 | 局部成立 | 局部成立 | 後端 record、private setter、IReadOnlyList 有保護；domain aggregate 仍是可變物件。前端 ref state 更是可變。 |
| 防禦性程式設計 | 成立度高 | 局部成立 | 後端有 validation、access denied、not found、conflict、lock guard；前端有 API error mapping，但細部輸入與 state invariant 仍可靠測試補強。 |
| 錯誤處理優雅 | 成立度高 | 局部成立 | 後端 result-to-response 明確；前端 validation error 與 session invalidation 有處理，但跨畫面錯誤策略仍可統一。 |
| 日誌埋點完備 | 未驗證 | 未驗證 | 後端有 HTTP logging 與 observability timing，但「完備」需要 logging policy 與實際營運需求驗證；前端沒有完整 telemetry。 |
| 可觀測性滿分 | 局部成立 | 不成立 | 後端 board read path 的 observability 很有意識；但 full tracing、dashboard、alerting 未必完整。前端目前不是 observability 重點。 |
| 易測性 | 成立度高 | 局部成立 | 後端 interface、in-memory repository、API factory 提升易測性；前端 E2E 完整，但 unit/component 可再補。 |
| 單元測試覆蓋率 100% | 未驗證且不應宣稱 | 未驗證且不應宣稱 | 目前沒有 coverage 數據；可見測試檔也不足以支持 100% 宣稱。 |
| 契約式設計 | 局部成立 | 局部成立 | API contracts、validation result、typed response 有契約精神；但不是 Design by Contract 工具或 formal contract。 |
| 前後端解耦 | 成立度高 | 成立度高 | 前端透過 HTTP API 與 typed contract 串接，後端不依賴前端；但 response shape 仍會影響 UI。 |
| 微服務邊界清晰 | 局部成立 | 不適用 | RonAuth / RonFlow 有系統邊界；RonFlow 本體不是微服務，較像 modular monolith。 |
| 領域驅動設計 | 成立度高 | 較不適用 | 後端有清楚 domain language 與 model；前端承接領域語言，但不是 DDD 的主要落點。 |
| 事件溯源 | 不成立 | 不適用 | Activity timeline 不是 event store；目前不能宣稱 event sourcing。 |
| CQRS 讀寫分離 | 成立度高 | 局部成立 | 後端 command/query/read store 清楚；前端有 command/query service 命名與用途，但不是完整 CQRS state architecture。 |
| 可擴展性 | 成立度高 | 局部成立 | 後端可透過 service、repository、decorator 擴充；前端可擴充，但需控制 App/useRonFlowBoard 膨脹。 |
| 可維護性 | 成立度高 | 局部成立 | 後端維護入口與責任清楚；前端核心流程集中讓修改路徑清楚，但大檔案會提高長期成本。 |
| 可移植性 | 局部成立 | 局部成立 | 後端 SQLite/in-memory 與 .NET host 相對可搬移；前端 Vite/Vue 可建置部署，但環境設定與 RonAuth/RonFlow API 仍需配套。 |
| 可讀性 | 成立度高 | 局部成立 | 後端命名與分層提升可讀性；前端局部檔案過長，閱讀時需要更多上下文。 |
| 低成本修改 | 局部成立 | 局部成立 | 分層讓許多變更有固定落點；但跨前後端 feature 仍會同時牽動 contract、UI、service、tests。 |
| 零技術債 | 不成立 | 不成立 | 任何持續演進中的專案都有取捨。RonFlow 有良性結構，也有待拆分與待驗證處。 |
| 文件與程式碼同步 | 局部成立 | 局部成立 | `ronflow-tech` 文件與程式碼實踐高度呼應；但同步需要持續維護，不能一次寫完就算成立。 |
| 註解精煉克制 | 成立度高 | 成立度高 | 目前程式碼主要靠命名與結構表達，沒有大量噪音註解。 |
| Git commit message 符合 Conventional Commits | 未驗證 | 未驗證 | 本次工作區外層 git history 不完整，不能評估。既有 devlog 有 `[doc]` 類記錄，但不是正式 Conventional Commits 結論。 |
| CI/CD 全綠 | 未驗證 | 未驗證 | 這次沒有 CI 執行紀錄，不能宣稱。 |
| Lint 零警告 | 未驗證 | 未驗證 | 前端 package script 未見明確 lint script；未執行 lint，不能宣稱零警告。 |
| SonarQube A 評 | 未驗證 | 未驗證 | 沒有 SonarQube 報告，不能宣稱。 |
| Code Smell 為零 | 不成立 | 不成立 | 「零」本身不合理。像大 composable、多 boolean result flags、composition root 持續膨脹都可視為需要觀察的 smell。 |
| 循環複雜度低於 3 | 未驗證 | 未驗證 | 沒有跑 complexity 工具；從部分方法看也不宜直接宣稱全專案低於 3。 |

## 7. 如果架構委員會真的要給分

我會這樣評：

```text
後端：A-。架構方向清楚，分層與可測性成熟，但不能宣稱事件溯源、零技術債或滿分可觀測性。
前端：B+。使用者流程、API 邊界與驗收測試方向正確，但主流程狀態與畫面 orchestration 需要繼續拆小。
整體：B+ 到 A- 之間。它不是完美專案，但已經不是玩具結構。
```

比較重要的是，RonFlow 現在的程式碼品質不是靠單一原則撐起來的，而是靠幾個互相支援的選擇：

- spec-first / acceptance-first 讓功能不是憑感覺長出來。
- 後端分層讓 domain 不被 HTTP、SQLite、observability 污染。
- API integration tests 與 E2E tests 讓使用者流程有回歸保護。
- 技術文件讓架構取捨不只停在腦中。

這些比「所有原則全數達標」更有價值。

## 8. 下一步應該怎麼改善

如果要把 RonFlow 往下一階段推，我會優先看這幾件事：

1. 拆分前端 [useRonFlowBoard.ts](../../code-frontend/src/composables/useRonFlowBoard.ts)，把 task detail、lifecycle list、lock、reminder、board movement 等行為逐步整理成更小的 composable。
2. 替前端 application service 與重要 composable 補更多 unit tests，讓 E2E 不必承擔所有回歸壓力。
3. 觀察 [CommandResults.cs](../../code-backend/RonFlow.Application/CommandResults.cs) 是否開始出現過多 boolean flag 組合，必要時改成更嚴格的 result type。
4. 將 [Program.cs](../../code-backend/RonFlow.Api/Program.cs) 的 service registration 逐步模組化，讓 composition root 保持可讀。
5. 若未來真的需要 event sourcing，再從目前 activity timeline 與 domain event language 往 event store 推進，而不是先宣稱自己已經是事件溯源架構。
6. 若要談 coverage、lint、SonarQube、complexity，就讓工具報告進入 devlog；沒有數據就不要把它們寫成戰功。

## 9. 總結

那段架構委員會風格的網路文字之所以好笑，是因為它把所有工程原則堆到一種荒謬的飽和狀態，好像專案只要能背出這串咒語，就會自然變得高品質。

RonFlow 這次自評得到的結論剛好相反：好的程式碼品質不是把所有原則都喊滿，而是知道哪些原則在這個專案、這個階段、這個風險下真的有用。

RonFlow 的後端值得肯定，前端方向也健康；但它仍然需要持續拆分、測試、量測與校正。這比宣稱「零技術債、滿分架構」更誠實，也更有工程價值。

2026/05/31。