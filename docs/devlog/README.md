# devlog

開發過程的思維紀錄。

## 目錄

- [專案起跑](#devlog-project-kickoff)
- [專案管理前置作業：建立 Backlog 與 Workflow](#devlog-project-management-setup)

<a id="devlog-project-kickoff"></a>
### 專案起跑

基於某種深植於靈魂的呼喚，我深深的被軟體工程所吸引。我不是資訊領域背景的，也不是甚麼天賦異稟的奇才，我只是**喜歡**這件事，從我第一個專案開始，我就意識到，軟體開發過程中不可避免的混亂，都會體現在程式碼中，最終化成埋藏在專案裡深處的地雷，在未來的某個時間點引爆。因此，在我連迴圈都寫得零零落落的菜鳥時期，我就在思考程式碼的結構、命名，乃至團隊開發流程等議題。

總之，這份動力驅使我在節奏緊湊的軟體產業裡摸爬滾打直到如今，我也何其有幸探索到諸如 [XP](../tech-base/xp.md)、[Scrum](../tech-base/scrum.md)、[Domain Modeling](../tech-base/domain-modeling.md)、[BDD](../tech-base/bdd.md)（[TDD](../tech-base/tdd.md)、[ATDD](../tech-base/atdd.md)）等深刻而優美的軟體工程的解決方案。在如今 AI 賦能的浪潮下，就像是終於為這顆運轉不休的引擎找到了合適的汽車，我終於可以開始親手實踐這些東西。

於是，這個專案就這樣誕生了。不知道這個專案能持續運作到甚麼時候，我看著那些靜靜躺在我的 [GitHub repo](../tech-base/github-repository.md) 的七十幾個專案，也許這個專案也是一樣，沒更新過幾次，就又會被新一輪生活忙亂攪起的渾水淤泥給掩埋，沉積在時間的長河裡。

也或許一切會有所不同。

特別感謝 [泰迪軟體](https://teddysoft.tw/) 的啟發，泰迪老師對知識的嚴謹和熱愛，以及不吝分享的個性，讓我得以深深體會到軟體工程領域的趣味。這個專案裡面提到的許多的軟體工程的知識和技術，也都是在泰迪的部落格、YouTube，或者工作坊裡面學到的。

歡迎所有熱愛軟體工程的夥伴們，一起討論各式各樣的議題。

那我們就開始吧。

2026/04/25 02:53。

<a id="devlog-project-management-setup"></a>
### 專案管理前置作業：建立 [Backlog](../tech-base/backlog.md) 與 [Workflow](../tech-base/workflow.md)

在我對敏捷初淺的理解內，[Backlog](../tech-base/backlog.md) 大致等同於待辦清單，把一個專案分解成若干個待辦事項，可以更清楚的掌握專案進度，如果使用 [Kanban](../tech-base/kanban.md) 等方式管理專案，更可以統計出每個流程的 [WIP](../tech-base/wip.md)，更能夠確認是哪個環節是瓶頸，進而挹注必要資源，提升產能。

至於 [Backlog](../tech-base/backlog.md) 裡面要寫甚麼，一個簡單的指標是「所有可能讓產品更接近目標的工作收斂到一個可排序、可討論、可取捨的清單裡」。

因此 [Backlog](../tech-base/backlog.md) 是服膺於某個目標之下的，在和 AI 討論過後，我先將目標暫定為「建立 RonFlow 的最小可用開源版本，使使用者可以自架系統，建立專案、建立任務，並透過簡單的 [看板](../tech-base/kanban.md) 追蹤任務狀態。同時，專案本身要展現清楚的 [.NET](../tech-base/dotnet.md) 架構、測試策略、開發紀錄與架構決策」。

從上述目標可以得知，RonFlow v0.1 至少會包含幾種工作類型：

- 產品功能，例如 Project、Task、[Kanban Board](../tech-base/kanban.md)
- 工程建設，例如 [.NET](../tech-base/dotnet.md) solution、[分層架構](../tech-base/layered-architecture.md)、測試專案、資料庫設定
- 架構決策，例如技術選型、專案結構、[Domain Model](../tech-base/domain-model.md) 邊界
- 測試與驗收，例如 [ATDD](../tech-base/atdd.md)、[Acceptance Criteria](../tech-base/acceptance-criteria.md)、[Integration Test](../tech-base/integration-test.md)
- 文件與開發紀錄，例如 README、tech-base、devlog、[ADR](../tech-base/adr.md)

接著，我需要替這些工作項目設計一個簡單的 [Workflow](../tech-base/workflow.md)。第一版暫定如下：

- `Backlog`：值得保存，但尚未準備執行的想法或工作項目。
- `Ready`：已經足夠清楚，可以被放進近期 [Sprint](../tech-base/sprint.md) 或直接開始處理的項目。
- `In Progress`：目前正在進行的工作。
- `Review`：已完成初步處理，等待檢查、補文件、整理測試或確認是否符合完成條件。
- `Done`：符合 [Definition of Done](../tech-base/definition-of-done.md)，可以視為完成的項目。

配合這次想導入的 [ATDD](../tech-base/atdd.md) 與 [Outside-In](../tech-base/outside-in.md) 開發模式，功能型 Backlog Item 會盡量依照以下方式推進：

- 定義需求與驗收條件
- 撰寫驗收測試
- 從使用者介面或 [API](../tech-base/api.md) 入口開始設計
- 推進到 [Application Layer](../tech-base/application-layer.md)
- 調整 [Domain Model](../tech-base/domain-model.md)
- 補足 [Infrastructure](../tech-base/infrastructure.md) 實作
- 補上必要的開發紀錄

不過，這套流程不會被機械式套用到所有工作項目。像是文件、研究、架構決策或專案結構調整，可能會有不同的完成方式。對我來說，[Workflow](../tech-base/workflow.md) 的目的不是製造儀式感，而是讓工作狀態可以被看見，讓我知道自己現在正在做什麼、卡在哪裡，以及下一步該往哪裡推進。

在和 AI 討論後，AI 推薦我直接使用 [GitHub Project](../tech-base/github-project.md)，對目前的專案進行管理。

![GitHub Project 建立畫面 1](../assets/github%20project%201.png)
![GitHub Project 建立畫面 2](../assets/github%20project%202.png)
按下新增之後，跳出的視窗，依照我這個專案開發的需求，選擇"kanban"feature。

![GitHub Project 建立畫面 3](../assets/github%20project%203.png)
會問你要不要把 issue 匯入。

![GitHub Project 建立畫面 4](../assets/github%20project%204.png)
建立完成就會跑到這個畫面。

![GitHub Project 建立畫面 5](../assets/github%20project%205.png)
透過 Add Item 來建立項目(底下會跳出輸入框讓你輸入 Item 的標題，但操作上沒有那麼直觀)

![GitHub Project 建立畫面 6](../assets/github%20project%206.png)
不知道為什麼，要選擇 [repo](../tech-base/github-repository.md)

![GitHub Project 建立畫面 7](../assets/github%20project%207.png)
大致上工作了一下。

至此，我們就完成了專案管理的前置作業。

2026/04/25 09:09。