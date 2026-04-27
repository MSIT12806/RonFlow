# 事件投影、經典查詢與 RonFlow v0.1 查詢策略

## 文件目的

本文整理 RonFlow 討論中關於以下主題的知識：

- 什麼是事件投影（Event Projection）
- 事件投影與 Event Sourcing 的關係
- 事件投影與經典查詢的差異
- 事件投影與經典查詢的優缺點比較
- RonFlow v0.1 建議採用的查詢與事件策略

---

# 一、什麼是事件投影？

事件投影（Event Projection）是：

> 把事件資料轉換成某種「方便查詢、顯示、統計或使用」的資料模型。

也可以說：

> Event 是事實紀錄；Projection 是把這些事實整理成可讀狀態。

---

## 1.1 用 RonFlow 舉例

假設系統發生這些事件：

```text
TaskCreated
TaskAssigneeChanged
TaskStateChanged
TaskCompleted
TaskMarkedUrgent
```

這些事件本身像流水帳：

```text
10:00 任務被建立
10:03 任務被指派給 Ron
10:10 任務移到 In Progress
11:30 任務被標記為 urgent
14:00 任務移到 Done
14:00 任務完成
```

但是使用者打開看板時，不想看這一串事件。  
使用者想看到的是：

```text
任務標題：建立看板
負責人：Ron
目前狀態：Done
是否緊急：是
完成時間：14:00
```

把前面那串事件整理成這個查詢狀態的過程，就叫做 **Projection**。

---

## 1.2 基本結構

事件投影可以理解成：

```text
Events
→ Projector
→ Read Model
```

其中：

| 名稱 | 意義 |
|---|---|
| Event | 已經發生的業務事實 |
| Projector | 負責讀取事件並更新查詢模型的程式 |
| Read Model / Projection | 為查詢、顯示、統計準備好的資料模型 |

例如：

```text
TaskCreated
TaskAssigneeChanged
TaskStateChanged
TaskCompleted
```

經過 projector 處理後，可以產生：

```text
TaskDetailView
KanbanBoardView
ProjectTaskListView
TaskActivityTimeline
```

---

# 二、事件投影不等於每次查詢都重播所有事件

這是最重要的觀念之一。

事件投影有兩種常見使用方式。

---

## 2.1 模式一：查詢時即時重播事件

這種方式是：

```text
讀取所有相關 events
→ replay events
→ 算出目前狀態
→ 回傳查詢結果
```

例如：

```text
TaskCreated
TaskAssigneeChanged
TaskStateChanged
TaskCompleted
```

重播後得到：

```text
CurrentState = Done
Assignee = Ron
CompletedAt = 14:00
```

這種方式可以用，但通常不適合高頻查詢。

### 適合情境

```text
除錯
稽核
歷史重建
特定 Aggregate 狀態還原
開發工具
少量資料的診斷工具
```

### 不適合情境

```text
使用者每次打開看板
大量資料列表
高頻 API 查詢
需要低延遲的頁面
```

---

## 2.2 模式二：事件發生時更新 Read Model

這是更常見的查詢用 projection。

例如：

```text
TaskCreated 發生
→ projector 新增 task_read_model 一筆資料

TaskStateChanged 發生
→ projector 更新 task_read_model.current_state

TaskCompleted 發生
→ projector 更新 task_read_model.completed_at

TaskMarkedUrgent 發生
→ projector 更新 task_read_model.is_urgent
```

這樣使用者查詢時，不需要重播事件，只要查已經整理好的 Read Model：

```sql
SELECT *
FROM task_read_model
WHERE project_id = @projectId;
```

所以日常查詢可以很快。

---

# 三、事件投影的用途

## 3.1 建立查詢模型

事件投影可以用來建立畫面需要的 Read Model，例如：

```text
KanbanBoardView
TaskDetailView
MyTasksView
UrgentTasksView
CompletedTasksView
```

---

## 3.2 建立活動紀錄

例如 `TaskActivityTimeline`。

事件：

```text
TaskCreated
TaskStateChanged
TaskRejected
TaskReopened
```

可以被投影成使用者可讀的活動紀錄：

```text
Ron 建立了任務「建立看板」
Ron 將任務從 Ready 移到 Active
QA 將任務退回，原因：驗收未通過
Ron 重新開啟已完成任務，原因：問題復發
```

---

## 3.3 建立統計資料

事件可以持續更新統計表，例如：

```text
CompletedTaskCount
RejectedCount
ReopenedCount
AverageCycleTime
UrgentTaskCount
```

這樣就不需要每次查詢時計算大量歷史資料。

---

## 3.4 重建 Read Model

如果 Read Model 壞掉，或統計邏輯改了，可以重新從事件重建：

```text
清空 projection table
→ replay events
→ 重建 read model
```

這是事件投影很大的價值之一。

---

# 四、事件投影與 Event Sourcing 的關係

事件投影常出現在 Event Sourcing 裡，但不是 Event Sourcing 專用。

---

## 4.1 Event Sourcing 裡的事件投影

在 Event Sourcing 中：

```text
Event Store = Source of Truth
Projection = 查詢模型
```

系統狀態的來源是事件。

也就是說，系統不以目前狀態資料表作為主要真相來源，而是以事件流為主要依據：

```text
TaskCreated
TaskStateChanged
TaskCompleted
TaskReopened
```

目前狀態由事件重播或投影得到。

---

## 4.2 非 Event Sourcing 系統也可以使用事件投影

即使不是完整 Event Sourcing，也可以使用事件投影。

例如：

```text
tasks table 是目前狀態
task_events table 是事件紀錄
task_activity_timeline 是由事件投影出來的查詢資料
```

這不是完整 Event Sourcing，但仍然可以使用事件投影。

---

# 五、經典查詢是什麼？

經典查詢是指：

> 直接從目前狀態資料表查詢資料。

例如 RonFlow 有：

```text
tasks
projects
workflows
workflow_states
users / members
```

查 Kanban Board 時直接查：

```sql
SELECT *
FROM tasks
WHERE project_id = @projectId
ORDER BY is_urgent DESC, priority DESC, updated_at DESC;
```

這種方式簡單、直覺、適合 MVP。

---

# 六、經典查詢的優點與缺點

## 6.1 優點

### 1. 簡單直覺

資料怎麼存，就怎麼查。

例如：

```text
Task.CurrentStateId
Task.AssigneeId
Task.Priority
Task.IsUrgent
```

直接查詢即可。

---

### 2. 開發成本低

不需要額外設計：

```text
Event Store
Projector
Projection Table
Replay 機制
Projection version
Projection rebuild
Eventually Consistency
```

所以比較快做出可用系統。

---

### 3. 資料一致性比較容易理解

Command 成功後直接更新目前狀態。

例如：

```text
ChangeTaskState
→ update tasks.current_state_id
→ commit
```

使用者下一次查詢通常可以馬上看到結果。

---

### 4. 除錯比較容易

資料表裡直接看到目前狀態。

例如：

```text
Task.CurrentStateId = Done
Task.CompletedAt = 2026-04-27
Task.IsUrgent = true
```

不需要先追事件流或 projector。

---

## 6.2 缺點

### 1. 歷史語意容易遺失

你只知道目前狀態，不一定知道怎麼變成這樣。

例如現在看到：

```text
Task.CurrentState = Active
```

但你不知道它是：

```text
從 Ready 進入 Active
從 Review 被退回
從 Done 被重新開啟
```

除非你另外做 Activity Log 或事件紀錄。

---

### 2. 複雜查詢可能越來越難維護

如果看板要顯示：

```text
目前狀態
上次被退回時間
退回次數
上次重開原因
是否曾經 urgent
目前 assignee
最近活動
```

經典查詢可能會變成大量 join、subquery、group by 或聚合查詢。

---

### 3. Read Model 容易被資料表結構綁死

如果 UI 要的是看板卡片，但資料表是正規化設計，就常常需要組裝很多資料。

例如：

```text
TaskCard = tasks + users + workflow_states + latest_activity + rejected_count
```

查詢可能逐漸複雜。

---

### 4. 事件驅動能力較弱

如果沒有明確事件紀錄，未來要做這些功能會比較困難：

```text
完成率統計
任務歷史重建
活動時間線
任務狀態變化分析
重建報表
```

---

# 七、事件投影的優點與缺點

## 7.1 優點

### 1. 適合建立查詢模型

你可以針對畫面設計資料。

例如 `KanbanBoardView` 可以直接長得像前端需要的資料：

```text
ProjectId
Columns[]
Tasks[]
AssigneeName
Priority
IsUrgent
LastRejectedAt
CompletedAt
```

查詢時不必每次重組。

---

### 2. 可以保留完整業務歷史

事件本身就是歷史。

你可以知道：

```text
Task 什麼時候建立
什麼時候指派
什麼時候進入 Review
什麼時候被退回
什麼時候完成
什麼時候重開
```

這很適合：

```text
Activity Timeline
Audit Log
Debugging
管理分析
```

---

### 3. 適合統計與報表

例如：

```text
RejectedCount
ReopenedCount
AverageCycleTime
CompletedTaskCount
UrgentTaskCount
```

這些可以由事件持續更新，而不是每次查詢時計算。

---

### 4. 可以重建 Read Model

如果 projection table 壞掉，或統計邏輯改了，可以：

```text
清空 projection
重播 events
重新建立 read model
```

---

### 5. 寫入模型與查詢模型可以分離

Domain Model 專注處理規則。

Read Model 專注服務畫面。

例如：

```text
Task Aggregate:
保護狀態變更規則

KanbanBoardView:
服務看板顯示

TaskActivityTimeline:
服務歷史顯示
```

這接近 CQRS 的精神。

---

## 7.2 缺點

### 1. 開發成本高

需要多做：

```text
事件儲存
事件處理器
projection table
投影更新邏輯
rebuild 機制
失敗重試
idempotency
```

對 v0.1 可能太重。

---

### 2. 一致性會變複雜

如果事件發生後 projection 是非同步更新，會有短暫延遲。

例如：

```text
使用者剛完成 Task
Task command 已成功
但 KanbanBoardView 還沒更新
```

使用者可能暫時看到舊資料。

這叫 eventual consistency。

---

### 3. Debugging 門檻較高

出問題時要查：

```text
Command 有沒有成功？
Event 有沒有存？
Projector 有沒有跑？
Projection table 有沒有更新？
是否重複處理？
是否漏處理？
```

比經典查詢複雜。

---

### 4. Projection 會有版本問題

如果事件格式改了，或 Read Model 規則改了，要處理：

```text
舊事件如何重播？
投影邏輯如何升級？
projection 是否需要 rebuild？
```

---

### 5. 查詢資料可能冗餘

為了查詢快，Read Model 可能會保存重複資料。

例如：

```text
TaskTitle
AssigneeName
StateName
ProjectName
```

這些可能在不同 projection 裡重複出現。

優點是查詢快，缺點是同步與一致性需要管理。

---

# 八、事件投影 vs 經典查詢比較表

| 面向 | 經典查詢 | 事件投影 |
|---|---|---|
| 開發難度 | 低 | 高 |
| 適合 MVP | 很適合 | 容易過重 |
| 查目前狀態 | 簡單 | 查 projection 也簡單，但要先建 projection |
| 查歷史 | 較弱，需要額外 log | 很強 |
| Activity Timeline | 需另外設計 | 很自然 |
| 報表統計 | 查詢可能複雜 | 可由事件持續更新 |
| 效能 | 初期通常好 | 查詢可很好，但寫入與投影較複雜 |
| 一致性 | 較直覺 | 可能 eventual consistency |
| 維護成本 | 較低 | 較高 |
| 重建 Read Model | 不容易 | 容易，若事件完整 |
| 除錯 | 看目前資料容易 | 追事件與 projector 較複雜 |
| 架構彈性 | 中 | 高 |
| 適合複雜領域 | 中 | 高，但成本高 |

---

# 九、RonFlow v0.1 建議策略

對 RonFlow v0.1，建議採用：

```text
Classic Query + Domain Events + Activity Log
```

也就是：

```text
經典查詢為主
Domain Events 記錄業務事實
Activity Log 提供使用者可讀歷史
Selective Projection 作為未來擴充
```

---

## 9.1 Classic Query 處理主要畫面

例如：

```text
KanbanBoardView
TaskDetailView
ProjectTaskListView
MyTasksView
UrgentTasksView
CompletedTasksView
```

先從目前狀態表查：

```text
tasks
projects
workflows
workflow_states
users / members
```

這樣 v0.1 開發速度最快，也最容易除錯。

---

## 9.2 Domain Events 記錄業務事實

例如：

```text
TaskCreated
TaskStateChanged
TaskCompleted
TaskRejected
TaskReopened
TaskAssigneeChanged
TaskPriorityChanged
TaskMarkedUrgent
TaskUnmarkedUrgent
```

這些事件不一定一開始都拿來投影，但要保留，因為它們是未來分析、投影、稽核、除錯的重要素材。

---

## 9.3 Activity Log 提供使用者可讀歷史

Activity Log 可以從 Domain Event 轉換而來，也可以在同一個 transaction 裡寫入。

例如：

```text
Ron 建立了任務「建立看板」
Ron 將任務從 Ready 移到 Active
QA 將任務退回，原因：驗收未通過
Ron 將任務標記為緊急
```

這不一定是完整 Event Sourcing，但足以支援 Task Activity Timeline。

---

## 9.4 經典查詢撐不住時，再做 Selective Projection

只有遇到這類情況才導入 projection：

```text
查詢需要大量 join、group by、subquery，效能變差
查詢邏輯高度依賴歷史事件
需要即時計算統計，例如 rejected count、cycle time
需要跨專案、跨使用者聚合資料
需要重建某種 read model
```

例如未來可以針對這些做 projection：

```text
ProjectProgressProjection
AssigneeWorkloadProjection
CycleTimeProjection
UrgentTaskDashboardProjection
RejectedTaskStatisticsProjection
```

---

# 十、RonFlow v0.1 決策草案

## 決策

RonFlow v0.1 採用：

```text
Classic Query + Domain Events + Activity Log
```

## 說明

- 日常查詢以目前狀態資料表進行經典查詢。
- Domain Events 用來表達業務事實。
- Activity Log 用來提供使用者可讀的歷史紀錄。
- 不在 v0.1 導入完整 Event Sourcing。
- 不全面使用事件投影。
- 當經典查詢難以支撐特定查詢或統計需求時，再針對該需求導入 Selective Projection。

---

## 優點

```text
降低 v0.1 開發複雜度。
查詢與除錯較直覺。
保留 Domain Events 作為未來 projection、稽核、分析與事件驅動功能的基礎。
避免過早導入完整 Event Sourcing。
```

---

## 代價

```text
某些複雜統計初期可能需要較複雜的 SQL。
Read Model 與事件歷史之間需要維持語意一致。
未來導入 projection 時，需要設計事件處理、投影重建與一致性策略。
```

---

# 十一、建議後續演進路線

## v0.1

```text
Classic Query + Domain Events + Activity Log
```

做法：

```text
1. Command 成功後更新 tasks 目前狀態。
2. 同時記錄 domain event / activity record。
3. 看板、列表、詳情頁用 tasks 等目前狀態表查詢。
4. Activity Timeline 用 events / activity table 查詢。
```

---

## v0.2 或以後

針對變複雜的查詢導入 Selective Projection：

```text
KanbanBoardProjection
UrgentTasksProjection
ProjectProgressProjection
AssigneeWorkloadProjection
CycleTimeProjection
RejectedTaskStatisticsProjection
```

---

## 未來需要時

再考慮：

```text
Event Sourcing
Projection rebuild
Outbox Pattern
Event versioning
Projection versioning
```

---

# 十二、一句話結論

對 RonFlow 目前階段：

> 不要在 v0.1 追求完整事件投影架構。  
> 先用經典查詢把主要 Read Model 做出來，同時保存 Domain Events / Activity Log，為未來 projection 留路。

這樣可以同時達成：

```text
能快速做出 MVP
能展現 DDD / Event Driven 思維
不會被 Event Sourcing 複雜度拖垮
能保留未來演進空間
```
