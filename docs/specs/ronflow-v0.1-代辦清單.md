# RonFlow v0.1 代辦清單

## 1. v0.1 產品定位

RonFlow v0.1 定位為：

> 個人可以順暢使用的專案 / 任務管理工具。

v0.1 的核心目標不是建立團隊協作平台，而是讓個人使用者可以順暢地建立專案、管理任務、推進任務狀態，並透過 Activity Timeline 回顧任務的生命歷程。

---

## 2. v0.1 核心判斷標準

v0.1 是否完成，應以以下問題判斷：

```text
我自己能不能真的每天打開 RonFlow 管理我的工作？
```

也就是說，RonFlow v0.1 應該讓一個人可以完成以下事情：

```text
1. 建立專案
2. 建立任務
3. 查看任務狀態
4. 拖曳任務推進流程
5. 編輯任務內容
6. 標記任務完成
7. 重新開啟已完成任務
8. 回顧任務的活動紀錄
```

---

## 3. v0.1 Scope

### 3.1 Included

RonFlow v0.1 應包含以下功能：

```text
1. Project List
2. Create Project
3. Default Workflow
4. Project Kanban Board
5. Create Task
6. Edit Task
7. Move Task State
8. Complete Task
9. Reopen Task
10. Reorder Task
11. Task Detail Drawer
12. Activity Timeline
13. Priority
14. Due Date
15. 基本 loading state
16. 基本 error handling
17. 基本 empty state
18. 基本自動測試覆蓋
```

### 3.2 Excluded

RonFlow v0.1 暫不包含以下功能：

```text
1. Project Member
2. Assignee
3. Permission / Role
4. Comment thread
5. Mention
6. Notification
7. Team Activity Feed
8. 多人即時同步
9. Sprint Planning
10. Sprint Review
11. Team Velocity
12. Burndown Chart
13. Attachment
14. 完整報表
```

---

## 4. v0.1 核心主軸

RonFlow v0.1 聚焦於四條主線：

```text
1. Task Flow
2. Task Detail
3. Activity Timeline
4. Personal Workflow Usability
```

---

## 5. Task Flow 代辦

### 5.1 目前已具備的基礎

```text
1. 使用者可以建立 Task。
2. 新建立的 Task 會進入 Todo。
3. 使用者可以在 Kanban Board 上看到 Task Card。
4. 使用者可以透過 drag & drop 變更 Task 狀態。
5. Task 可以從非 Done 狀態移動到 Done。
6. Task 進入 Done 時，系統會記錄完成時間。
```

### 5.2 待補強

```text
1. 拖曳任務到不同欄位時，狀態應正確更新。
2. 拖曳後若沒有放到有效欄位，Task Card 應回到原欄位與原位置。
3. 狀態變更 API 失敗時，Task Card 應回到原欄位。
4. 狀態變更 API 失敗時，畫面應顯示錯誤訊息。
5. Done → 非 Done 時，系統應視為 Reopen。
6. 非 Done → Done 時，系統應視為 Complete。
7. 同欄位內應支援 Task 排序。
8. 跨欄位移動時，應支援指定落點順序。
```

### 5.3 建議 Domain Events

```text
TaskCreated
TaskStateChanged
TaskCompleted
TaskReopened
TaskReordered
```

---

## 6. Task Detail 代辦

### 6.1 Task Detail Drawer 應顯示

```text
1. Task Title
2. Description
3. Current State
4. Priority
5. Due Date
6. CreatedAt
7. CompletedAt
8. Activity Timeline
```

### 6.2 Task Detail Drawer 應支援操作

```text
1. 編輯 Task Title
2. 編輯 Description
3. 修改 Priority
4. 修改 Due Date
5. 關閉 Drawer
```

### 6.3 暫不支援

```text
1. Assignee
2. Reviewer
3. Watcher
4. Comment
5. Attachment
```

---

## 7. Activity Timeline 代辦

### 7.1 Activity Timeline 定位

Activity Timeline 是 Task 的生命紀錄。

它應讓使用者可以理解：

```text
1. 這個任務是什麼時候建立的？
2. 什麼時候開始做？
3. 什麼時候進入審查？
4. 什麼時候完成？
5. 後來是否又重新打開？
6. 期間改過哪些重要資訊？
```

### 7.2 v0.1 應支援的 Activities

```text
TaskCreated
TaskTitleChanged
TaskDescriptionChanged
TaskPriorityChanged
TaskDueDateChanged
TaskStateChanged
TaskCompleted
TaskReopened
TaskReordered
```

### 7.3 Activity Timeline 中文顯示範例

```text
已建立任務
已更新任務標題
已更新任務描述
已將優先度改為「高」
已設定到期日為 2026/05/20
已將狀態從「待處理」移至「進行中」
已完成任務
已重新開啟任務
已調整任務順序
```

---

## 8. Personal Workflow Usability 代辦

v0.1 應優先處理個人每天使用時會遇到的體驗問題。

### 8.1 建議功能

```text
1. 快速建立任務
2. Enter 送出建立任務
3. 建立任務後可以繼續建立下一個
4. Task Card 顯示 Priority
5. Task Card 顯示 Due Date
6. 過期任務有明顯提示
7. Done 欄位可以收合
8. Board 記住上次開啟的 Project
```

---

## 9. Sprint 2 建議範圍

### 9.1 Sprint 2 Goal

```text
讓 Task 成為可被個人實際管理的工作項目，而不只是可拖曳的卡片。
```

### 9.2 Sprint 2 必做

```text
1. 補齊 Move Task State On Board 的缺漏測試。
2. 實作 Task Description。
3. 實作 Task Priority。
4. 實作 Task Due Date。
5. 實作 TaskCompleted 行為。
6. 實作 TaskReopened 行為。
7. 擴充 Activity Timeline。
8. Drawer 開啟期間，Task 更新時應即時同步。
```

### 9.3 Sprint 2 可選

```text
1. 同欄位 Task 排序。
2. 跨欄位移動時指定落點順序。
3. Done 欄位收合。
4. 快速建立多筆任務。
5. 過期任務提示。
```

### 9.4 Sprint 2 不建議納入

```text
1. 完整帳號 / 權限 / Project Member 管理。
2. Assignee。
3. Sprint / Iteration 管理。
4. 自訂 Workflow。
5. WIP hard limit。
6. Notification。
7. Comment thread。
8. Attachment。
```

---

## 10. 測試代辦

### 10.1 Move Task State On Board 缺漏測試

```text
1. 拖曳後若沒有放到有效欄位，Task Card 應回到原欄位與原位置。
2. 狀態變更 API 失敗時，Task Card 應回到原欄位並顯示錯誤訊息。
3. Drawer 已開啟時若外部拖曳成功，Drawer 應同步最新狀態、CompletedAt 與 Activity Timeline。
```

### 10.2 Task Detail 測試

```text
1. 使用者可以在 Drawer 查看 Task Description。
2. 使用者可以在 Drawer 修改 Task Title。
3. 使用者可以在 Drawer 修改 Task Description。
4. 使用者可以在 Drawer 修改 Priority。
5. 使用者可以在 Drawer 修改 Due Date。
6. 修改成功後，Drawer 應顯示最新資料。
7. 修改成功後，Activity Timeline 應新增對應紀錄。
8. 修改失敗時，畫面應顯示錯誤訊息，且不應錯誤覆蓋原資料。
```

### 10.3 Complete / Reopen 測試

```text
1. Task 從非 Done 狀態移動到 Done 時，應記錄 TaskCompleted。
2. Task 從非 Done 狀態移動到 Done 時，應設定 CompletedAt。
3. Task 從 Done 移回非 Done 狀態時，應記錄 TaskReopened。
4. Task 從 Done 移回非 Done 狀態時，Current CompletedAt 不應繼續顯示為目前完成時間。
5. Activity Timeline 應保留過去曾完成與重新開啟的紀錄。
```

### 10.4 Activity Timeline 測試

```text
1. 建立 Task 後，Activity Timeline 應顯示已建立任務。
2. 修改 Title 後，Activity Timeline 應顯示已更新任務標題。
3. 修改 Description 後，Activity Timeline 應顯示已更新任務描述。
4. 修改 Priority 後，Activity Timeline 應顯示已變更優先度。
5. 修改 Due Date 後，Activity Timeline 應顯示已設定或更新到期日。
6. 移動狀態後，Activity Timeline 應顯示狀態變更。
7. 完成任務後，Activity Timeline 應顯示已完成任務。
8. 重新開啟任務後，Activity Timeline 應顯示已重新開啟任務。
```

---

## 11. 優先順序建議

### P0：先完成，否則 v0.1 不算順暢

```text
1. Move Task State On Board 缺漏測試
2. Task Description
3. Task Priority
4. Task Due Date
5. TaskCompleted
6. TaskReopened
7. Activity Timeline 擴充
8. Drawer 即時同步
```

### P1：v0.1 體驗明顯加分

```text
1. 同欄位 Task 排序
2. 跨欄位移動時指定落點順序
3. Task Card 顯示 Priority
4. Task Card 顯示 Due Date
5. 過期任務提示
6. 快速建立多筆任務
```

### P2：可延後到 v0.2

```text
1. Done 欄位收合
2. Board 記住上次開啟的 Project
3. WIP limit warning
4. 自訂 Workflow
5. 簡易統計報表
```

---

## 12. v0.1 完成定義

RonFlow v0.1 可以視為完成，當它滿足以下條件：

```text
1. 使用者可以建立 Project。
2. 使用者可以建立 Task。
3. 使用者可以編輯 Task 的基本內容。
4. 使用者可以透過 Kanban Board 推進 Task 狀態。
5. 使用者可以完成 Task。
6. 使用者可以重新開啟 Task。
7. 使用者可以透過 Activity Timeline 回顧 Task 的重要變化。
8. 常見失敗情境有合理錯誤處理。
9. 主要 flow 有自動測試覆蓋。
10. 一個人可以用 RonFlow 管理自己的日常專案與任務。
```
