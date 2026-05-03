# Outside-In 與事件風暴的順序調整：從 Read Model / View 開始是否更合理？

## 背景

在 RonFlow 的事件風暴模擬中，我們原本採用比較典型的 DDD / Event Storming 路線：

```text
Event → Command → Actor → Rule → Aggregate → Read Model
```

也就是先從「業務中發生了什麼事」開始，逐步推導出命令、角色、規則、模型與查詢需求。

但在進行到 Modeling & Delivery 階段時，我們提出一個重要問題：

> 為什麼不先從 Read Model / View 開始討論？  
> Outside-In 開發不是強調先從使用者能看見什麼、能操作什麼開始嗎？

這個問題很關鍵，因為它指出了事件風暴與 Outside-In 開發的切入點差異。

---

# 一、事件風暴與 Outside-In 的切入點不同

## 事件風暴的典型切入點

事件風暴通常從這個問題開始：

```text
這個領域中發生了哪些重要事件？
```

它的關注點是：

- 業務流程
- 狀態變化
- 事件順序
- 規則與例外
- 不同角色對流程的理解
- 領域語言的對齊

典型推導方向是：

```text
Event
→ Command
→ Actor
→ Business Rule
→ Aggregate
→ Read Model
```

這種方式適合用在：

- 業務流程複雜
- 角色多
- 規則不清楚
- 狀態變化重要
- 團隊需要建立共同語言
- 需要釐清不同部門對同一件事的理解差異

例如：

- 請假審核
- 訂單流程
- 交易查核
- 案件管理
- 醫療照護流程
- 政府行政審查流程

---

## Outside-In 的典型切入點

Outside-In 通常從這些問題開始：

```text
使用者想完成什麼？
使用者需要看到什麼？
使用者會在哪個畫面做什麼操作？
```

它的關注點是：

- 使用者目標
- 使用者可見畫面
- 使用者操作
- 系統對外行為
- 驗收條件
- 產品最小可用價值

典型推導方向是：

```text
User Goal
→ View / Read Model
→ User Action
→ Command
→ Event
→ Domain Model
```

這種方式適合用在：

- MVP 開發
- 一人專案
- 需要快速交付可用功能
- 前端體驗很重要
- 想用 ATDD / BDD 驅動開發
- 想避免過度建模

---

# 二、兩種方法不衝突

事件風暴與 Outside-In 並不是互斥方法。

它們只是方向不同。

## 事件風暴方向

```text
Event
→ Command
→ Actor
→ Rule
→ Aggregate
→ Read Model
```

## Outside-In 方向

```text
User Goal
→ View / Read Model
→ Action
→ Command
→ Event
→ Aggregate
```

兩條路最後都會匯合到：

- Command
- Event
- Business Rule
- Domain Model
- Read Model
- Acceptance Criteria
- Backlog

差別只是：**先從業務事件切入，還是先從使用者可見成果切入。**

---

# 三、以 RonFlow 為例

如果從事件風暴開始，我們會先問：

```text
任務從建立到完成，發生了哪些業務事件？
```

可能得到：

```text
TaskCreated
TaskStateChanged
TaskCompleted
TaskRejected
TaskReopened
TaskAssigneeChanged
TaskMarkedUrgent
```

再往後推導：

```text
CreateTask
ChangeTaskState
RejectTask
ReopenTask
ChangeTaskAssignee
MarkTaskUrgent
```

最後才得到：

```text
KanbanBoardView
TaskDetailView
TaskActivityTimeline
```

如果從 Outside-In 開始，我們會先問：

```text
使用者打開 RonFlow v0.1，需要看到什麼？
```

可能先得到：

```text
Kanban Board
Task Detail
Project Task List
Create Task Form
Task Activity Timeline
Urgent / Priority Filter
```

然後再問：

```text
使用者在這些畫面上會做什麼操作？
```

得到：

```text
新增任務
拖曳任務卡
退回任務
重新開啟任務
變更負責人
標記緊急
調整優先度
```

再往後推導：

```text
CreateTask
ChangeTaskState
RejectTask
ReopenTask
ChangeTaskAssignee
MarkTaskUrgent
ChangeTaskPriority
```

最後回到事件：

```text
TaskCreated
TaskStateChanged
TaskCompleted
TaskRejected
TaskReopened
TaskAssigneeChanged
TaskMarkedUrgent
TaskPriorityChanged
```

---

# 四、對 RonFlow v0.1，Outside-In 可能更直觀

RonFlow 的目標不是純粹做領域研究，而是要打造一個可以使用的專案管理工具。

它同時有兩個目標：

```text
1. 做出可用的 v0.1 MVP。
2. 展示 DDD / Event Storming / TDD / ATDD / Outside-In 的工程思維。
```

因此如果完全採用事件風暴優先，會有幾個優點：

- 業務語意清楚
- 狀態變化嚴謹
- Domain Event 思維完整
- 容易寫出有深度的 devlog / ADR

但也有缺點：

- 離使用者可見功能較遠
- 容易過度分析
- 容易先討論模型，而不是先確認 v0.1 畫面價值
- 容易把 MVP 拉得過重

對 RonFlow v0.1 而言，較合理的策略是：

> 先用事件風暴理解業務語意，再用 Outside-In 決定 v0.1 真正要交付什麼。

或者更適合 MVP 的方式：

> 先用 Outside-In 定義使用者可見成果，再用事件風暴支撐背後的行為模型。

---

# 五、建議改成 Hybrid 流程

RonFlow 可以採用混合流程：

```text
Outside-In Delivery Mapping
→ Command / Event Mapping
→ Domain Model
→ Backlog / ADR
```

也就是：

```text
使用者目標
→ 畫面 / Read Model
→ 使用者操作
→ Command
→ Event
→ Business Rule
→ Aggregate / Entity
→ Backlog / ADR
```

這樣可以保留事件風暴的深度，也避免偏離 MVP 交付。

---

# 六、重新調整會議 4

原本會議 4 是：

```text
Modeling & Delivery
```

內容包含：

- Aggregate / Entity 候選
- Read Model / View 候選
- Backlog Items
- ADR 清單
- Spike 清單
- Acceptance Criteria

但經過討論後，會議 4 可以改成：

```text
Outside-In Delivery Mapping
```

目標變成：

```text
1. 使用者在 v0.1 需要看到哪些畫面 / Read Model？
2. 每個畫面支援哪些操作？
3. 每個操作對應哪些 Command？
4. 每個 Command 對應哪些 Event / Rules？
5. 最後整理 Backlog / ADR。
```

這樣會更符合 RonFlow 的 MVP 目標與 Outside-In 開發模式。

---

# 七、建議的新會議 4 結構

## 第 1 段：v0.1 使用者可見成果

先問：

```text
使用者打開 RonFlow v0.1，需要看到什麼？
```

候選畫面 / Read Model：

```text
Project List
Project Detail / Kanban Board
Task Detail
Create Task Form
Task Activity Timeline
Urgent / Priority Filter
```

這一段的重點不是 UI 細節，而是確認 v0.1 的使用者可見價值。

---

## 第 2 段：每個畫面的使用者操作

例如：

### Kanban Board

```text
- Create Task
- Move Task State
- Mark Urgent
- Change Assignee
- Change Priority
```

### Task Detail

```text
- Edit Title / Description
- Reject Task
- Reopen Task
- View Activity
```

這一段的重點是從畫面反推使用者行為。

---

## 第 3 段：操作轉 Command / Event

把使用者操作接回前面 Behavior Mapping 的成果。

例如：

| User Action | Command | Event |
|---|---|---|
| 新增任務 | CreateTask | TaskCreated |
| 拖曳任務卡 | ChangeTaskState | TaskStateChanged |
| 拖到 Done | ChangeTaskState | TaskStateChanged + TaskCompleted |
| 退回任務 | RejectTask | TaskStateChanged + TaskRejected |
| 重新開啟任務 | ReopenTask | TaskStateChanged + TaskReopened |
| 變更負責人 | ChangeTaskAssignee | TaskAssigneeChanged |
| 標記緊急 | MarkTaskUrgent | TaskMarkedUrgent |

---

## 第 4 段：Backlog / ADR / Acceptance Criteria

最後把結果整理成開發項目。

### Backlog Items

```text
[Feature] 建立 Project
[Feature] 建立 Task
[Feature] 顯示 Kanban Board
[Feature] 拖曳 Task 改變狀態
[Feature] 任務進入 Done 時產生 TaskCompleted
[Feature] 退回 Task
[Feature] 重新開啟 Task
[Feature] 變更 Task Assignee
[Feature] 標記 Task 為 Urgent
[Feature] 顯示 Task Activity Timeline
```

### ADR

```text
[ADR] 使用 TaskStateChanged 搭配 WorkflowState.Category 表達任務狀態流轉
[ADR] 保留 TaskCompleted 作為獨立事件
[ADR] RejectTask / ReopenTask 作為獨立 Command
[ADR] v0.1 暫不實作完整 WorkflowTransition Rule
```

### Acceptance Criteria

```text
Scenario: 使用者可以建立 Task
Scenario: 使用者可以在 Kanban Board 上看到 Task
Scenario: 使用者可以拖曳 Task 改變狀態
Scenario: Task 進入 Done 狀態時，系統記錄完成
Scenario: 使用者可以退回 Review 中的 Task
Scenario: 使用者可以重新開啟已完成 Task
```

---

# 八、原本事件風暴成果仍然有價值

改成 Outside-In 不是否定前面事件風暴。

前面的事件風暴已經幫助我們釐清：

- 為什麼不是寫死 `TaskStarted`
- 為什麼需要 `TaskStateChanged`
- 為什麼需要 `WorkflowState.Category`
- 為什麼 `TaskCompleted` 要保留
- 為什麼 `RejectTask` / `ReopenTask` 要獨立
- 為什麼 `Urgent` 不等於 `Priority`

這些都是後續 Outside-In 開發的重要支撐。

更精準地說：

> 事件風暴幫我們建立業務語意的深度，Outside-In 幫我們決定 v0.1 要優先交付什麼。

---

# 九、結論

對 RonFlow v0.1 這種產品開發而言，先從 Read Model / View 開始會更直觀。

事件風暴沒有錯，但它比較適合領域探索。  
Outside-In 更適合 MVP 交付。

因此建議後續會議改成：

```text
Outside-In Delivery Mapping：
從使用者可見畫面回推 Command / Event / Model / Backlog
```

新的推導方向是：

```text
User Goal
→ View / Read Model
→ User Action
→ Command
→ Event
→ Rule
→ Domain Model
→ Backlog / ADR
```

這樣既能保留 DDD 與事件風暴的深度，又能避免分析過度，讓 RonFlow 更快走向可用版本。
