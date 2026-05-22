# RonFlow 的 DDD 實踐

## 為什麼這篇文章值得寫
DDD 很容易被誤解成「多畫幾個 aggregate 與 value object」，但它真正重要的是用領域語言與邊界去整理問題。RonFlow 的價值，在於它不是先從技術框架出發，而是先把流程管理、身分認證、通知等問題分開理解。

## 這個技術概念是什麼
DDD 關心的是：
- 問題領域中的核心概念是什麼
- 這些概念之間如何互動
- 哪些責任屬於同一個 bounded context
- 哪些事情應該被拆到 supporting domain 或外部 context

它不是單純的 class design，而是整個系統邊界與語言的設計方法。

## 它背後的設計精神
### 1. 模型要反映問題，而不是反映資料表
如果系統只剩 CRUD table thinking，就很難清楚表達流程狀態、生命週期、提醒、擁有權這些真正的領域概念。

### 2. bounded context 比萬用 shared model 更重要
RonFlow 與 RonAuth 的拆分，就是一個很好的例子。認證與流程管理是不同問題，不應共用一個混雜的 identity-flow model。

### 3. 用 ubiquitous language 讓討論能對齊
Project、Task、WorkflowState、Archived、Trashed、Reminder、Owner 這些詞不是隨便命名，它們是系統溝通語言的一部分。

## 這樣做的優點
### 1. 核心模型比較穩定
當語言與邊界先被整理好，系統在成長時比較不容易失去方向。

### 2. 跨 domain 的協作比較清楚
RonAuth 負責 authentication，RonFlow 負責 owner boundary 與流程規則，責任切分比較容易說清楚。

### 3. 比較容易對應未來演進方向
多人協作、多租戶、policy-based access control、projection，這些未來需求都會依賴清楚的 domain 邊界。

## 代價與限制
### 1. 前期需要花時間整理語言與邊界
這比直接開始寫 CRUD 慢，但它換來的是較少的長期混亂。

### 2. 如果沒有持續維護語言，模型會逐漸漂移
DDD 不是一次建模完就結束，而是要持續校正系統語言。

### 3. 不是所有功能都需要重度建模
DDD 有價值，但也要知道哪些地方是 core domain，哪些地方只需要 supporting capability。

## RonFlow 裡是怎麼實作的
### 核心 domain 放在流程管理本身
RonFlow 的核心概念集中在 project、task、workflow、lifecycle、reminder 這些流程系統要處理的事情，而不是放在帳號管理（`Project` aggregate、`Task` aggregate、`Task.ChangeState`、`Task.Archive`、`Task.MoveToTrash`、`Task.AddReminder`）。

### RonAuth 被視為 supporting domain
登入、註冊、session、token 由 RonAuth 負責，RonFlow 只信任它提供的已驗證使用者身分（RonFlow 端對應 `Program.Main` 內的 JWT Bearer 設定，以及 `AuthenticatedControllerBase.TryGetCurrentUserId`）。

### Owner boundary 成為核心模型的一部分
當 RonFlow 進入 authenticated single-owner 模式後，Project 的 OwnerId 不再只是技術欄位，而是業務邊界的一部分。這讓「誰擁有資料、誰能操作資料」成為明確模型語意（`Project.OwnerId`、`Project.Create`、`ProjectAccessService.GetOwnedProject`）。

### 雙狀態軸反映更細緻的領域觀點
Workflow state 與 lifecycle state 分開，代表 RonFlow 沒有把所有任務狀態混成一條線，而是承認任務在流程位置與生命週期位置上是兩種不同維度（`Task.CurrentState`、`Task.LifecycleState`、`Task.ChangeState`、`Task.Archive`、`Task.RestoreFromArchive`、`Task.MoveToTrash`、`Task.RestoreFromTrash`）。

## 這件事對 RonFlow 代表什麼
RonFlow 目前最值得肯定的 DDD 實踐，不是用了多少 pattern，而是已經開始把核心問題、supporting capability、系統邊界、與後續演進方向分開思考。這讓它不只是功能作品，也是一個可持續擴張的領域模型雛形。

## 總結
RonFlow 的 DDD 實踐，核心在於把流程管理視為 core domain，把認證視為 supporting domain，把模型與邊界建立在領域語言之上。這比單純把 class 命名得比較漂亮更重要，也更能支撐系統未來成長。
