# RonFlow 的 workflow state / lifecycle state 雙狀態軸設計

## 為什麼這篇文章值得寫
這個設計值得被單獨寫出來，因為它不是「Task 多兩個欄位」而已，而是 RonFlow 怎麼理解任務狀態的核心建模方式。

如果系統只用一條狀態線去表達所有事情，通常很快就會把「任務目前做在哪個流程欄位」與「任務目前是否還在主要工作視野」混在一起，最後變成一條語意混雜的 mega state machine。RonFlow 把它拆成 workflow state 與 lifecycle state 兩條軸，代表它承認這兩件事本來就是不同問題。

## 這個技術概念是什麼
RonFlow 裡的 Task 同時有兩個狀態維度：

- workflow state：Task 在工作流程中的欄位位置，例如 Todo、Active、Review、Done
- lifecycle state：Task 是否仍位於主要工作視野，例如 ActiveRecord、Archived、Trashed

這個定義不是事後從程式碼倒推，而是一開始就在 spec 裡講清楚。`ronflow-core-flow-spec` 明確寫出：Archive / Trash 不屬於 workflow state，而屬於 lifecycle state；Archived / Trashed task 也必須保留原本的 workflow state，讓還原時能回到原欄位。

## 它背後的設計精神
### 1. 把流程位置與生命週期分開建模
Task 在 Board 上落在哪一欄，與 Task 是否已被封存、移到垃圾桶，本質上不是同一種狀態。前者描述工作推進位置，後者描述資料是否仍在主要工作視野。

### 2. 避免狀態爆炸
如果把 archived / trashed 直接塞進 workflow state，狀態集合會開始混雜「流程進度」與「資料可見性」，之後 restore、board query、completedAt、排序規則都會一起變難談。

### 3. 讓還原語意保持自然
被封存或丟進垃圾桶的 Task，不應失去它原本在流程中的位置語意。保留 workflow state，restore 時只要把 lifecycle state 改回 ActiveRecord，就能自然回到原欄位。

## 這樣做的優點
### 1. Board 與 Archive / Trash view 的責任更清楚
Board 只關心目前仍在工作視野中的 Task；封存區與垃圾桶則是 lifecycle projection，不需要把整個 workflow 重新定義一次。

### 2. workflow rule 與 lifecycle rule 可以獨立演進
像 `done` 會影響 `CompletedAt`，這是 workflow 語意；`archive` / `trash` 會影響 `ArchivedAt` / `TrashedAt` 與顯示位置，這是 lifecycle 語意。兩者不用互相污染。

### 3. read model 比較容易對齊 UI
同一份 task data 可以同時支撐 board、archived list、trash list，而不是為了不同畫面重新發明不同型別的狀態字串。

## 代價與限制
### 1. 每個 use case 都要清楚知道自己在改哪一條軸
`ChangeState`、`Archive`、`MoveToTrash`、`RestoreFromArchive`、`RestoreFromTrash` 不能混著寫，否則模型很快又會退化成模糊狀態。

### 2. query 與排序規則要多想一步
Board 查詢要過濾 `ActiveRecord`；archive / trash list 要依 lifecycle timestamp 排序；restore 還要回到原 workflow column 的尾端。這些都比單一狀態欄位多一點設計成本。

### 3. 前後端都要維持同一套語意
如果前端把 archived 當成 workflow column，看板與 task detail 就會和後端模型脫鉤。因此這不是只有 domain 端要知道的概念。

## RonFlow 裡是怎麼實作的
### spec 先定義雙狀態軸
在 [ronflow-core-flow-spec.md](../specs/ronflow-core-flow-spec.md) 的 `Task State Axes` 章節，RonFlow 已先定義 Task 應同時具有 workflow state 與 lifecycle state，並明確規定 archive / trash 不屬於 workflow state。

### Domain 直接把兩條軸分開
在 [Workflow.cs](../../code-backend/RonFlow.Domain/Workflow.cs) 裡，workflow state 是獨立的 `WorkflowState` model；在 [Models.cs](../../code-backend/RonFlow.Domain/Models.cs) 裡，lifecycle state 則是 `TaskLifecycleState` enum。

而 [Task.cs](../../code-backend/RonFlow.Domain/Task.cs) 同時持有：

- `CurrentState`
- `LifecycleState`
- `CompletedAt`
- `ArchivedAt`
- `TrashedAt`

這些欄位分別承接不同軸線的語意。`ChangeState` 只處理 workflow state 與 completed / reopened 行為；`Archive`、`MoveToTrash`、`RestoreFromArchive`、`RestoreFromTrash` 只處理 lifecycle state 與對應時間戳記。

### Application read model 會依 lifecycle state 決定畫面投影
在 [CoreFlowReadModels.cs](../../code-backend/RonFlow.Application/CoreFlowReadModels.cs) 中：

- `CreateProjectBoard` 只會取 `LifecycleState == ActiveRecord` 的 task 進 board column
- `CreateLifecycleTaskList` 會依傳入的 lifecycle state 建立 archived / trashed list

這表示雙狀態軸不只存在於 aggregate 內部，也已經影響 query model 與畫面投影結構。

### Restore use case 直接吃到這個設計紅利
在 [RestoreArchivedTaskCommandService.cs](../../code-backend/RonFlow.Application/RestoreArchivedTaskCommandService.cs) 裡，還原時會先找出同一個 workflow column 內、仍為 `ActiveRecord` 的任務排序尾端，再呼叫 `task.RestoreFromArchive(...)`。

這個流程之所以簡單，就是因為 archive 沒有破壞原本的 workflow state。還原時不需要猜 Task 應該回哪一欄，因為模型本來就保留了那個資訊。

### Frontend 也承認這是兩條軸
在 [useRonFlowBoard.ts](../../code-frontend/src/composables/useRonFlowBoard.ts) 裡，Task detail mode 被拆成 `active`、`archived`、`trashed`，同時又保留 workflow state 相關操作。前端也另外使用 archived / trashed 專屬 query，而不是把它們當成 board column 的延伸。

這說明雙狀態軸已經不是單一後端欄位，而是前後端共同遵守的產品模型。

## 為什麼我會覺得它值得提及
我會把它列進 Wish 的「already」，是因為這個設計代表 RonFlow 已經開始從「功能狀態怎麼湊得出來」走向「領域語意怎麼被正確表達」。

很多 task system 一開始會把所有狀態塞成一列字串，短期看起來快，但之後只要碰到 archive、trash、restore、completed、board projection、activity timeline，就會開始互相牽連。RonFlow 現在把 workflow 與 lifecycle 拆開，代表它已經先避開了一條很常見的建模死路。

這也是為什麼我認為它值得被當成架構設計，而不只是 implementation detail。因為它影響的不是單一 API，而是 spec、domain model、application query、restore 規則、frontend view model 這整條設計鏈。

## 總結
workflow state / lifecycle state 雙狀態軸的價值，不在於名詞比較漂亮，而在於它讓 RonFlow 可以用更乾淨的模型同時處理流程推進與資料生命週期。這讓 board、archive、trash、restore、completed 這些需求不必互相扭曲，也讓系統之後更容易繼續演化。