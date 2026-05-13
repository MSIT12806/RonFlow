# RonFlow Event Storming：Task Removal & Recovery

## 1. 本輪事件風暴主題

```text
封存、移到垃圾桶、永久刪除、還原的語意與邊界
```

本輪事件風暴目標是釐清 RonFlow v0.1 中，使用者如何安全地將不需要、不想顯示、誤建立或重複建立的 Task 移出主要工作視野，並保留必要的反悔與還原機制。

---

## 2. 參與角色

### 2.1 Domain Expert

負責描述真實任務管理情境、使用者需求與領域語意。

關注問題：

```text
1. 使用者在什麼情境下會想讓 Task 從主要畫面消失？
2. 完成、封存、垃圾桶、永久刪除的語意差異是什麼？
3. 使用者如何理解「還原」？
4. 哪些任務仍有保留價值？
5. 哪些任務比較像錯誤資料或重複資料？
```

### 2.2 PO / RD

負責產品方向、功能切分、技術可行性、Domain Model 與 Sprint 範圍。

關注問題：

```text
1. 哪些事件應該成為 Domain Event？
2. Archive / Trash 是否應該是 workflow state？
3. Sprint 3 應該納入哪些功能？
4. 哪些功能應該延後？
5. 如何控制 scope，避免 v0.1 膨脹？
```

### 2.3 QA / UIUX

負責規格明確化、DoD、真實操作問題、誤觸風險、互動設計，以及避免錯誤操作造成垃圾資料或不必要伺服器負擔。

關注問題：

```text
1. 哪些操作容易誤觸？
2. 哪些操作應該放在 Task Card，哪些應該放在 Task Detail Drawer？
3. 使用者如何知道操作後 Task 去哪裡？
4. 是否需要二次確認？
5. 如何讓系統方便直覺，又避免資料遺失？
```

---

## 3. 背景前提

本輪討論建立在以下前提上：

```text
1. RonFlow v0.1 是個人可以順暢使用的專案 / 任務管理工具。
2. RonFlow v0.1 不處理多人協作、權限、即時同步。
3. Task 建立後立即持久化，不採用延遲寫入。
4. 使用者可能會建立錯誤、重複或不再需要的 Task。
5. 系統需要提供安全的移除與還原機制。
6. 系統不應讓使用者輕易永久刪除重要資料。
```

---

## 4. Domain Expert：使用者語意分析

在個人任務管理裡，「不想再看到一個 Task」其實有不同原因：

```text
1. 這件事做完了，但我暫時還想在 Done 欄位看到。
2. 這件事做完很久了，我想保留紀錄，但不想繼續出現在看板。
3. 這件事本來要做，但後來決定不做。
4. 這件事是我剛剛建錯的，或者是重複資料。
5. 這件事完全沒價值，我想永久清掉。
```

因此需要區分四個概念：

```text
Complete：任務完成。
Archive：任務有保留價值，但不再出現在主要工作視野。
Move To Trash：任務是錯誤、重複、不需要的資料，先移出主要視野並保留反悔機會。
Permanent Delete：使用者明確決定永久移除資料。
```

核心語意：

```text
完成不是封存。
封存不是垃圾桶。
垃圾桶不是永久刪除。
```

---

## 5. PO / RD：Domain Model 初步設計

Archive、Trash 不應該被設計成 Workflow State。

RonFlow 的 Task 狀態應分成兩條軸線：

### 5.1 第一軸：Workflow State

Workflow State 表示任務在工作流程中的狀態，也就是 Board 上的欄位。

```text
Todo
Active
Review
Done
```

### 5.2 第二軸：Lifecycle State

Lifecycle State 表示任務是否出現在主要工作視野，以及它目前屬於正常、封存、垃圾桶或永久刪除狀態。

```text
ActiveRecord
Archived
Trashed
Deleted
```

語意：

```text
ActiveRecord：正常任務，可出現在 Board / List。
Archived：封存任務，不出現在主要 Board，但可在封存區查看與還原。
Trashed：垃圾桶任務，不出現在主要 Board，但可在垃圾桶查看與還原。
Deleted：永久刪除，不可還原。
```

### 5.3 Task Model 概念草案

```text
Task
- Id
- ProjectId
- Title
- Description
- WorkflowState
- SortOrder
- LifecycleState
- CreatedAt
- CompletedAt
- ArchivedAt?
- TrashedAt?
- DeletedAt?
```

---

## 6. Archive Task：封存任務

### 6.1 Domain Expert 語意

封存的語意是：

```text
這個 Task 不是錯誤資料，也不是垃圾。
它仍然有紀錄價值，只是我目前不想在主要工作視野看到。
```

常見使用情境：

```text
1. Done 欄位累積太多已完成任務。
2. 某些任務已經失去當前行動價值，但仍想保留紀錄。
3. 某些 Project 已經暫停，任務先收起來。
4. 使用者想讓 Board 保持乾淨。
```

### 6.2 Commands

```text
ArchiveTask
RestoreArchivedTask
```

### 6.3 Domain Events

```text
TaskArchived
TaskRestoredFromArchive
```

### 6.4 Rules

```text
1. 只有正式存在的 Task 可以封存。
2. 已封存的 Task 不出現在 Project Kanban Board。
3. 已封存的 Task 不出現在一般 My Tasks List。
4. 已封存的 Task 出現在 Archived Tasks View。
5. 已封存的 Task 可以還原。
6. Archive 不改變 Task 原本的 workflow state。
7. 還原後，Task 回到原本 workflow state。
8. 還原後，Task 放在該 workflow column 的最後。
9. TaskArchived / TaskRestoredFromArchive 應記錄到 Activity Timeline。
```

### 6.5 QA / UIUX 操作設計

封存操作入口：

```text
1. 封存操作放在 Task Detail Drawer 的更多選單中。
2. 不要放在 Task Card 上。
```

理由：

```text
1. 避免誤觸。
2. 使用者封存前應先看過詳細內容。
3. 封存是低頻操作，不應佔據主畫面。
```

封存後操作回饋：

```text
已封存任務
[復原]
```

Archived View 最少應顯示：

```text
1. Task Title
2. Project Name
3. 原 workflow state
4. ArchivedAt
5. 還原操作
```

Archived View 中的 Task Detail Drawer：

```text
1. 可以開啟。
2. 以 read-only mode 顯示。
3. 可查看 Activity Timeline。
4. 不允許編輯內容。
5. 不允許移動狀態。
6. 提供還原操作。
```

---

## 7. Move Task To Trash：移到垃圾桶

### 7.1 Domain Expert 語意

垃圾桶的語意是：

```text
這個 Task 可能是錯的、重複的、不需要的。
我希望它離開主要工作視野，但仍保留反悔機會。
```

常見使用情境：

```text
1. 剛剛建立錯 Task。
2. 建立了重複 Task。
3. 任務完全不需要做。
4. 標題內容寫錯太多，使用者不想修，想移除。
```

### 7.2 Commands

```text
MoveTaskToTrash
RestoreTrashedTask
```

### 7.3 Domain Events

```text
TaskMovedToTrash
TaskRestoredFromTrash
```

### 7.4 Rules

```text
1. 正常任務可以移到垃圾桶。
2. 已封存任務也可以移到垃圾桶，但入口可以先不做。
3. 移到垃圾桶後，不出現在 Project Kanban Board。
4. 移到垃圾桶後，不出現在一般 My Tasks List。
5. 移到垃圾桶後，出現在 Trash View。
6. 垃圾桶中的 Task 可以還原。
7. Trash 不改變 Task 原本的 workflow state。
8. 還原後，Task 回到原本 workflow state。
9. 還原後，Task 放在該 workflow column 的最後。
10. TaskMovedToTrash / TaskRestoredFromTrash 應記錄到 Activity Timeline。
```

### 7.5 QA / UIUX 操作設計

第一層文案應為：

```text
移到垃圾桶
```

不建議第一層文案只顯示：

```text
刪除
```

原因：

```text
1. 「刪除」會讓使用者以為資料立即消失。
2. 「移到垃圾桶」更能表達這是可逆操作。
3. 可降低使用者焦慮。
```

Move To Trash 操作入口：

```text
1. 放在 Task Detail Drawer 的更多選單中。
2. 不要放在 Task Card。
3. 不要放在滑鼠 hover 快捷按鈕。
```

Move To Trash 前是否需要確認：

```text
不需要二次確認。
```

理由：

```text
1. Move To Trash 是可逆操作。
2. 每次跳確認會讓工具變笨重。
3. 透過明確文案與 toast 復原即可降低風險。
```

Move To Trash 後 toast：

```text
已移到垃圾桶
[復原]
```

若使用者點擊復原：

```text
觸發 TaskRestoredFromTrash。
```

Trash View 中的 Task Detail Drawer：

```text
1. 可以開啟。
2. 以 read-only mode 顯示。
3. 可查看 Activity Timeline。
4. 不允許編輯內容。
5. 不允許移動狀態。
6. 提供還原操作。
```

---

## 8. Permanent Delete：永久刪除

### 8.1 Domain Expert 語意

永久刪除是高風險操作。

語意是：

```text
我確定這筆 Task 不再需要，也不需要再還原。
```

這不是日常流程，而是清理垃圾桶時才會做。

### 8.2 Command

```text
DeleteTaskPermanently
```

### 8.3 Domain Event

```text
TaskDeletedPermanently
```

### 8.4 PO / RD 判斷

Sprint 3 建議先不做永久刪除。

理由：

```text
1. Move To Trash 已經解決誤建任務從主畫面消失的問題。
2. Restore 已經提供反悔機制。
3. 永久刪除需要額外確認、資料保留策略與測試。
4. 對 v0.1 來說不是最小必要功能。
5. 避免早期產品出現不可逆資料遺失風險。
```

若未來要做 Permanent Delete，應限制：

```text
1. 只能從 Trash View 操作。
2. 不允許從 Board 操作。
3. 不允許從一般 Task Detail Drawer 操作。
4. 必須二次確認。
```

### 8.5 QA / UIUX 要求

Permanent Delete 若納入，必須符合：

```text
1. 只能在 Trash View 操作。
2. 不出現在 Task Card。
3. 不出現在一般 Task Detail Drawer。
4. 操作文案要明確：「永久刪除」。
5. 需要二次確認。
6. 確認文案要提醒「此操作無法復原」。
```

建議：

```text
v0.1 先不做永久刪除。
```

理由：

```text
垃圾資料會累積，但對早期產品來說，比誤刪重要資料更安全。
```

---

## 9. Archive vs Trash vs Complete vs Permanent Delete

| 行為 | 使用者語意 | 是否可還原 | 是否出現在 Board | 對應 View |
|---|---|---:|---:|---|
| Complete | 任務做完 | 可 Reopen | 仍可出現在 Done | Board |
| Archive | 有保留價值，但不想看 | 可還原 | 不出現 | Archived View |
| Move To Trash | 錯誤、重複、不需要 | 可還原 | 不出現 | Trash View |
| Permanent Delete | 永久移除資料 | 不可還原 | 不出現 | 不出現 |

---

## 10. 還原規則

### 10.1 Domain Expert 觀點

使用者還原任務時，通常期待它回到原本的位置附近，但不一定記得原本精確順序。

最重要的是：

```text
不要還原後找不到。
```

### 10.2 PO / RD 決策

還原規則簡化為：

```text
1. Archive / Trash 不改變 Task 原本的 workflow state。
2. 還原時只將 LifecycleState 改回 ActiveRecord。
3. 還原後，Task 回到原本 workflow state。
4. 還原後，Task 放在該 workflow column 的最後。
5. Activity Timeline 記錄還原事件。
```

概念：

```text
Task.WorkflowState = Active
Task.LifecycleState = Trashed
```

還原時：

```text
Task.LifecycleState = ActiveRecord
```

Board 重新查詢時，Task 自然回到 Active 欄位最後。

### 10.3 QA / UIUX 操作回饋

還原後，使用者需要知道它去哪裡。

還原後 toast 可顯示：

```text
已還原任務到「進行中」
```

或：

```text
已還原任務
[前往查看]
```

如果是從 Trash View 或 Archived View 還原，建議提供：

```text
[前往查看]
```

---

## 11. Activity Timeline 規則

### 11.1 Domain Expert 觀點

封存、移到垃圾桶、還原，都是 Task 生命週期的一部分，因此 Activity Timeline 應保留。

### 11.2 PO / RD 決策

```text
1. Archived / Trashed 狀態下，Task Detail Drawer 仍可查看 Activity Timeline。
2. Archived Task 可查看，不可編輯，先只允許還原。
3. Trashed Task 可查看，不可編輯，先只允許還原。
```

理由：

```text
Archived / Trashed 任務已經不屬於主要工作流。
允許編輯會增加語意複雜。
```

### 11.3 QA / UIUX 設計

Archived / Trash View 中的 Task Detail Drawer 使用 read-only mode。

顯示提示：

```text
此任務已封存
```

或：

```text
此任務位於垃圾桶
```

並提供主要操作：

```text
還原
```

Trash View 若未來加入永久刪除，則提供次要高風險操作：

```text
永久刪除
```

---

## 12. 本輪確定的 Domain Events

```text
TaskArchived
TaskRestoredFromArchive
TaskMovedToTrash
TaskRestoredFromTrash
```

---

## 13. 本輪暫定但不納入 Sprint 3 的 Domain Event

```text
TaskDeletedPermanently
```

---

## 14. 本輪確定的 Commands

```text
ArchiveTask
RestoreArchivedTask
MoveTaskToTrash
RestoreTrashedTask
```

---

## 15. 本輪暫定但不納入 Sprint 3 的 Command

```text
DeleteTaskPermanently
```

---

## 16. 本輪確定的 Rules

```text
1. Archive / Trash 不屬於 workflow state，而屬於 task lifecycle state。
2. Archive / Trash 不改變 Task 原本的 workflow state。
3. Archived Task 不出現在 Project Kanban Board。
4. Trashed Task 不出現在 Project Kanban Board。
5. Archived Task 出現在 Archived Tasks View。
6. Trashed Task 出現在 Trash View。
7. Archived Task 可以還原。
8. Trashed Task 可以還原。
9. 還原後，Task 回到原本 workflow state。
10. 還原後，Task 放在該 workflow column 的最後。
11. Archived / Trashed Task 的 Activity Timeline 保留。
12. Archived / Trashed Task 可以開啟 read-only Drawer。
13. Archived / Trashed Task 不允許編輯內容、移動狀態或排序。
14. Task Card 不直接提供 Archive / Move To Trash 操作。
15. Archive / Move To Trash 操作放在 Task Detail Drawer 的更多操作中。
16. Move To Trash 是可逆操作，不需要二次確認。
17. Permanent Delete 若未來實作，只能從 Trash View 觸發，且必須二次確認。
```

---

## 17. QA / UIUX DoD 草案

### 17.1 Archive Task DoD

```text
1. 使用者可以從 Task Detail Drawer 的更多操作中封存 Task。
2. Task Card 不應直接顯示封存操作。
3. 封存後，Task 不應出現在 Project Kanban Board。
4. 封存後，Task 應出現在 Archived Tasks View。
5. 封存後，Activity Timeline 應包含「已封存任務」。
6. Archived Tasks View 中可以開啟 read-only Task Detail Drawer。
7. Archived Task 可以被還原。
8. 還原後，Task 應回到原本 workflow state。
9. 還原後，Task 應顯示在該 workflow column 的最後。
10. 還原後，Activity Timeline 應包含「已還原封存任務」。
```

### 17.2 Move Task To Trash DoD

```text
1. 使用者可以從 Task Detail Drawer 的更多操作中將 Task 移到垃圾桶。
2. Task Card 不應直接顯示移到垃圾桶操作。
3. 第一層操作文案應為「移到垃圾桶」，不應只顯示「刪除」。
4. 移到垃圾桶後，Task 不應出現在 Project Kanban Board。
5. 移到垃圾桶後，Task 應出現在 Trash View。
6. 移到垃圾桶後，Activity Timeline 應包含「已移到垃圾桶」。
7. 移到垃圾桶後，系統可顯示 toast：「已移到垃圾桶」與「復原」。
8. Trash View 中可以開啟 read-only Task Detail Drawer。
9. Trashed Task 可以被還原。
10. 還原後，Task 應回到原本 workflow state。
11. 還原後，Task 應顯示在該 workflow column 的最後。
12. 還原後，Activity Timeline 應包含「已從垃圾桶還原」。
```

### 17.3 Permanent Delete DoD，若未來納入

```text
1. 使用者只能從 Trash View 永久刪除 Task。
2. 永久刪除前，系統必須要求二次確認。
3. 確認文案必須包含「此操作無法復原」。
4. 永久刪除後，Task 不應出現在 Board、Archived View、Trash View。
5. 永久刪除後，Task 不可還原。
```

---

## 18. Sprint 3 建議切法

### 18.1 Sprint 3 Goal

```text
讓使用者可以安全地把不需要或不想顯示的任務移出主要工作視野，並能在誤操作後還原。
```

### 18.2 Sprint 3 Scope

```text
1. Task LifecycleState
2. Archive Task
3. Restore Archived Task
4. Archived Tasks View
5. Move Task To Trash
6. Restore Trashed Task
7. Trash View
8. Archived / Trashed Task read-only Drawer
9. Activity Timeline 記錄封存、移到垃圾桶與還原事件
10. 移到垃圾桶後 toast 復原
```

### 18.3 Sprint 3 Out of Scope

```text
1. Permanent Delete
2. Auto clean trash
3. Trash retention policy
4. Bulk archive
5. Bulk move to trash
6. Search archived / trashed tasks
7. Editing archived / trashed tasks
8. Moving archived / trashed tasks between workflow states
```

---

## 19. 本輪事件風暴結論

本輪事件風暴完成了 Task Removal & Recovery 的初版領域設計。

目前定案方向：

```text
Archive 和 Trash 都保留，但語意不同。
```

Archive 用於：

```text
有保留價值、但不想出現在主要工作視野的 Task。
```

Trash 用於：

```text
錯誤、重複、不需要的 Task，但仍保留還原機會。
```

Permanent Delete：

```text
暫緩，不放入 Sprint 3。
```

Sprint 3 建議主題：

```text
Task Removal & Recovery
```

Sprint 3 建議成果：

```text
使用者可以封存任務、還原封存任務、移到垃圾桶、從垃圾桶還原，
並且 Archived / Trashed 任務不干擾主要 Board，但仍可查看生命紀錄。
```

---

## 20. 下一輪事件風暴建議主題

```text
Task Marking：Star / Blocked / Tags
```

下一輪可討論：

```text
1. Star 是否代表重要或關注？
2. Blocked 是否應該是 workflow state，還是 task flag？
3. Tag 是否需要跨專案共用？
4. Task Card 應如何顯示標記？
5. Personal Task List 如何依標記篩選？
```
