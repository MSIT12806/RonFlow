# RonFlow 的 CQRS 實踐

## 為什麼這篇文章值得寫
CQRS 常被理解成一定要搭配 event sourcing 或極度複雜的分散式架構，但 RonFlow 展示的是一種更務實的版本：在沒有過度神化 CQRS 的前提下，仍然把讀與寫責任分開，讓行為與查詢各自清楚。

## 這個技術概念是什麼
CQRS 的核心很簡單：
- Command 負責改變系統狀態
- Query 負責讀取系統狀態
- 兩者的模型、責任與最佳化方向可以不同

它不強迫所有系統都要做成分散式，但鼓勵把「改變」與「查詢」分開思考。

## 它背後的設計精神
### 1. 寫入與讀取本來就是不同問題
建立專案、改變任務狀態、加入提醒，關心的是驗證規則與狀態改變；讀取專案列表、看板、任務詳情，關心的是回傳適合 UI 使用的資料形狀。這兩件事不必綁死在同一個 model。

### 2. 不要讓查詢需求反向污染寫入模型
如果所有東西都只靠單一 domain object 直接回給前端，很容易讓 aggregate 開始承擔過多查詢責任。

### 3. 先做出有價值的分離，而不是追求教科書式複雜度
RonFlow 的 CQRS 比較偏實用主義：有 command service、有 query service、有 read store，但沒有為了 CQRS 而把整個系統拆得不必要地複雜。

## 這樣做的優點
### 1. Command 與 Query 的意圖更清楚
看到 `CreateProjectCommandService` 與 `GetProjectsQueryService`，就很清楚它們各自的責任與方向。

### 2. 讀模型可以更靠近 UI 需要
Query service 可以直接回傳適合顯示的 view model，而不用硬把 UI 需求塞回 domain aggregate。

### 3. 寫模型可以專注在規則與狀態變更
Command service 的注意力放在驗證與改變，而不是順便長出一堆查詢格式整理。

## 代價與限制
### 1. 類別數量會增加
因為 command、query、result、view model 會被分開，所以檔案數量比單一 service 模式多。

### 2. 要維護 read model 與 write model 的一致性
即使不做 event sourcing，仍然要思考 read store 與 command side 的一致性更新。

### 3. 小功能若全部硬拆，也可能過重
CQRS 不是拆越細越好，而是要看系統複雜度與實際價值。

## RonFlow 裡是怎麼實作的
### Command side
RonFlow 有一系列 command service，例如：
- CreateProject
- CreateTask
- ChangeTaskState
- UpdateTask
- ArchiveTask
- MoveTaskToTrash
- CreateTaskReminder

這些服務的重點是檢查規則、改變狀態、產生 command output，而不是直接處理畫面需求。
（`CreateProjectCommandService.Create`、`CreateTaskCommandService.Create`、`ChangeTaskStateCommandService.Change`、`ArchiveTaskCommandService.Archive`、`CreateTaskReminderCommandService.Create`）

### Query side
RonFlow 也有清楚的 query service，例如：
- GetProjects
- GetProjectBoard
- GetTaskDetail
- GetArchivedTasks
- GetTrashedTasks

它們會從 read store 取資料，再轉成對前端友善的 view model。
（`GetProjectsQueryService.Get`、`GetProjectBoardQueryService.Get`、`GetTaskDetailQueryService.Get`、`GetArchivedTasksQueryService.Get`、`GetTrashedTasksQueryService.Get`）

### Owner boundary 被推進 command 與 query 兩側
這次 auth 整合之後，CQRS 的價值更明顯：
- query 只回傳 current user 可以看的資料
- command 只允許 current user 操作自己擁有的資料

所以 current user 與 owner scope 不是只在 controller 判斷，而是分別進到 command side 與 query side。
（`ProjectAccessService.GetOwnedProject`、`GetProjectBoardQueryService.Get`、`ChangeTaskStateCommandService.Change`）

## 這件事對 RonFlow 代表什麼
CQRS 讓 RonFlow 更適合長大。未來若加入 projection、報表、多人協作、event-driven integration，讀側與寫側本來就可能朝不同方向演進。現在先把這條路鋪好，後面就比較不會痛苦。

## 總結
RonFlow 的 CQRS 實踐不是為了追求架構時髦，而是為了把狀態改變與資料查詢分成兩種不同責任。這讓 command 與 query 都能保持清楚，也為之後更進一步的 projection 與整合留下空間。
