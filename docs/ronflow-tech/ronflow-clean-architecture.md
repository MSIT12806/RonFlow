# RonFlow 的 Clean Architecture 實踐

## 為什麼這篇文章值得寫
很多專案都會說自己用了 clean architecture，但實際上常常只是多切幾個資料夾。RonFlow 比較值得記錄的地方，在於它把層次切分與責任邊界真的落成了可辨識的程式結構。

## 這個技術概念是什麼
Clean Architecture 的核心不是資料夾名稱，而是依賴方向與責任分工：
- Domain 放最核心的業務概念
- Application 放 use case 與流程協調
- Infrastructure 放資料庫、外部服務與技術細節
- API / UI 則作為輸入輸出邊界

內層不應依賴外層，核心邏輯不應被框架或資料庫綁死。

## 它背後的設計精神
### 1. 讓核心邏輯可持續演化
真正會一直變的，不一定是資料庫或 Web framework，而是使用者流程與商業規則。因此應該把變動最有價值的那塊，從技術細節中抽離出來。

### 2. 讓 use case 與技術細節分開討論
如果 application service 一開始就直接長成 controller + SQL + DTO 混在一起，那之後要談業務規則會非常困難。

### 3. 用層次抵抗耦合，而不是只靠命名習慣
RonFlow 的分層是有具體責任差異的，而不是把同樣的程式搬到不同資料夾裡。

## 這樣做的優點
### 1. 比較容易看懂系統的責任分工
看到一個檔案在 Domain、Application、Infrastructure，通常就能先推測它應該做什麼、不應該做什麼。

### 2. 測試可以更精準地落在不同層次
Domain、Application、API integration、E2E 可以各自承擔不同責任，不必把所有問題都丟給同一層測試。

### 3. 對外部系統整合更有彈性
像 RonAuth、Web Push、SQLite 這些外部整合，都不需要直接滲入核心 domain。

## 代價與限制
### 1. 小型功能也會有一定結構成本
分層本身就有抽象成本，對非常小的功能來說，會比 transaction script 重一些。

### 2. 若沒有良好 discipline，很容易只剩形式上的分層
把檔案搬進 `Application` 不代表它真的維持 application 層責任。

### 3. DTO、view model、command result 會增加一些樣板結構
這是可讀性與清楚邊界換來的代價。

## RonFlow 裡是怎麼實作的
### Domain：放核心模型與規則
例如 `Project`、`WorkflowState`、`Task` 等模型，負責表達流程管理本身的核心概念，而不是處理 HTTP 或資料存取（`Project.Create`、`Project.Touch`、`Task.ChangeState`、`Task.Archive`、`Task.MoveToTrash`）。

### Application：放 command / query 與 use case 編排
像 CreateProject、GetProjects、ChangeTaskState、CreateTaskReminder 這些服務，集中描述使用者操作如何被轉成系統行為（`CreateProjectCommandService.Create`、`GetProjectsQueryService.Get`、`ChangeTaskStateCommandService.Change`、`CreateTaskReminderCommandService.Create`）。

### Infrastructure：放 SQLite、推播發送器、持久化細節
RonFlow 用 Infrastructure 包住儲存與外部技術整合，讓 Application 不需要直接依賴資料庫實作（`SqliteProjectRepository.GetProjects` / `Add` / `Update`、`CoreFlowJsonSerializer.Serialize` / `DeserializeProject`）。

### API：作為輸入邊界
Controller 的責任主要是接 HTTP request、轉成 command/query、處理回應碼與授權邊界，而不是塞滿商業邏輯（`ProjectsController.GetProjects` / `CreateProject` / `GetBoard`、`AuthenticatedControllerBase.TryGetCurrentUserId`）。

### Frontend：作為另一個外部輸入輸出面
Vue 前端負責使用者互動、狀態呈現、auth orchestration 與呼叫 API，但不直接承擔後端 business rule（`useRonFlowAuth.initialize` / `login` / `register`、`App.vue` 的 `onMounted` / `onLogin` / `onRegister`）。

## 這件事對 RonFlow 代表什麼
Clean Architecture 在 RonFlow 不是單純為了漂亮，而是讓這個系統可以持續承接更多主題，例如 supporting domain、多人協作、projection、通知、background service，而不會每增加一個功能就把系統攪成一團。

## 總結
RonFlow 的 clean architecture 實踐，核心價值在於把依賴方向與責任切分具體落成了程式結構。這讓系統比較能隨著需求成長，而不是在每次擴充時都重新把技術細節與業務規則攪在一起。
