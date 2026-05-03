# 事件風暴會議節奏檢討與改善建議

## 背景

在 RonFlow 的事件風暴練習中，我們完成了兩輪討論：

1. **第一輪：任務生命週期事件發散**

   * 找出 Task 從建立到完成可能發生的候選 Domain Events。
2. **第二輪：v0.1 最小流程收斂**

   * 將具體狀態事件抽象成 `TaskStateChanged`
   * 確認 `WorkflowState.Category` 的必要性
   * 保留 `TaskCompleted`、`TaskRejected`、`TaskReopened`
   * 將 `TaskAssigneeChanged` 從主流程中抽出
   * 初步區分 `Priority` 與 `Urgent`

這些內容具有分析價值，但作為一場真實會議，長度與認知負荷明顯偏高。

\---

# 一、主要檢討

## 1\. 會議長度不適合作為真實工作坊節奏

這次討論如果放在真實會議中，與會者很可能在中後段感到疲勞。

原因不是內容沒有價值，而是一次處理了太多層次：

* 事件排序
* 產品取捨
* 平台化抽象
* 未來擴充
* 資料設計暗示
* 業務語意判斷
* 架構方向推敲

這些議題都重要，但不應該在同一場事件風暴中全部展開。

\---

## 2\. 同一輪會議混入太多決策層次

第二輪原本目標是：

> 梳理 RonFlow v0.1 的最小流程。

但實際討論中同時處理了：

* `TaskStateChanged`
* `WorkflowState.Category`
* `TaskCompleted` 衍生事件
* `CompletedAt`
* `TaskRejected`
* `TaskReopened`
* `Urgent` / `Priority`
* `TaskAssigneeChanged`
* 子任務完成傳播
* Regression task
* Role-specific workflow
* Current Action Owner

這些其實可以拆成數場不同性質的會議。

其中一部分屬於事件風暴，一部分已經進入產品設計或架構設計。

\---

## 3\. 每個問題都太快進入完整分析

真實工作坊中，主持人不應該讓每個問題都當場完整展開。

比較好的做法是：

> 先判斷這個問題是否服務於本次會議目標。  
> 若不服務，先放入 Parking Lot。

這次我們對許多未來功能展開太多討論，例如：

* 子任務完成傳播
* Regression task
* Current Action Owner
* Role-specific workflow
* 完整 Interruption Workflow
* `CompletedAt` 實際儲存位置

這些都很有價值，但不一定要在事件風暴當場解決。

\---

## 4\. Mermaid 圖適合會後整理，不適合每一步都即時完整輸出

Mermaid 很適合用來整理事件風暴結果，放進 GitHub 文件、devlog 或 architecture docs。

但如果在會議過程中每一小段都輸出完整 Mermaid 圖，會讓節奏變慢。

真實事件風暴更適合：

* 即時使用便利貼、白板、Miro、FigJam 或 Excalidraw
* 會議中保持圖像簡單
* 會後再整理成 Mermaid

Mermaid 的最佳用途應該是：

> 會後文件化，而不是會中逐步畫完整架構圖。

\---

# 二、改善原則

## 1\. 每場會議只設定一個主要輸出物

不要讓同一場會議同時產出事件、流程、架構、資料模型與 backlog。

比較好的方式是：

|會議|主要輸出物|
|-|-|
|事件發散會議|候選 Domain Events|
|流程排序會議|事件時間線|
|平台抽象會議|Workflow / State / Category 模型|
|Command Mapping 會議|Command → Event 對照|
|設計整理會議|ADR / Backlog / Acceptance Criteria|

每場只追求一個核心成果。

\---

## 2\. 使用 Timebox 控制討論

建議每場控制在 50 分鐘內：

```text
5 分鐘：確認範圍
15 分鐘：發散或討論
15 分鐘：整理與分組
10 分鐘：決策
5 分鐘：Parking Lot 與下一步
```

如果是個人 side project，可以更短：

```text
20～30 分鐘處理一個主題
```

例如：

* 今天只討論 `TaskAssigned` 是否為必經事件
* 今天只討論 `TaskCompleted` 是否需要獨立事件
* 今天只討論 `Review`、`Rejected`、`Reopened` 的差異

\---

## 3\. 使用 Parking Lot 控制範圍

會議中若出現重要但不屬於本輪目標的議題，應先放入 Parking Lot。

例如：

```markdown
## Parking Lot

- 子任務完成時是否自動完成主任務
- Regression bug 是否應建立 linked task
- Current Action Owner 是否納入 v0.1
- Role-specific workflow 是否為未來功能
- Urgent 是否需要完整 Interruption Workflow
- CompletedAt 應存在 Task Entity 還是 Read Model
```

Parking Lot 的作用不是忽略問題，而是避免它破壞本輪會議焦點。

\---

## 4\. 分開事件風暴與架構設計

事件風暴主要關心：

* 發生了什麼事件
* 事件如何排列
* 有哪些分支
* 有哪些業務規則
* 有哪些疑問

架構設計才處理：

* `TaskStateChanged` 要不要記 snapshot
* `CompletedAt` 放在哪裡
* `WorkflowState.Category` enum 怎麼命名
* 是否產生衍生事件
* Entity / Aggregate / Read Model 怎麼設計

這兩種討論可以銜接，但不應該完全混在一起。

\---

# 三、建議的會議拆分方式

## 會議 1：任務生命週期事件發散

### 目標

找出 Task 從建立到完成過程中，可能發生的 Domain Events。

### 產出

* 候選事件清單
* 初步事件分組
* Open Questions
* Parking Lot

### 不討論

* 架構抽象
* 資料庫設計
* API
* Aggregate
* 衍生事件設計

\---

## 會議 2：事件時間線與最小流程

### 目標

排出 RonFlow v0.1 的任務最小流程。

### 產出

例如：

```text
TaskCreated
→ TaskStateChanged
→ TaskCompleted
```

以及分支：

```text
Review → Rejected
Done → Reopened
```

### 不討論

* `CompletedAt` 要存在哪裡
* 子任務完成傳播
* Regression linked task
* 完整 workflow 設計

\---

## 會議 3：平台化抽象

### 目標

把固定狀態事件抽象成平台型工具需要的 Workflow / State / Category 模型。

### 產出

* `TaskStateChanged`
* `Workflow`
* `WorkflowState`
* `WorkflowState.Category`
* 固定事件到抽象事件的轉換表

例如：

|業務語意事件|平台化事件|
|-|-|
|TaskStarted|TaskStateChanged|
|TaskSubmittedForReview|TaskStateChanged|
|TaskCompleted|TaskStateChanged + TaskCompleted|
|TaskRejected|TaskStateChanged + TaskRejected|
|TaskReopened|TaskStateChanged + TaskReopened|

\---

## 會議 4：Command → Event Mapping

### 目標

找出使用者或系統的意圖如何造成事件。

### 產出

|Command|Event|
|-|-|
|CreateTask|TaskCreated|
|ChangeTaskState|TaskStateChanged|
|CompleteTask|TaskStateChanged + TaskCompleted|
|RejectTask|TaskStateChanged + TaskRejected|
|ReopenTask|TaskStateChanged + TaskReopened|
|ChangeTaskAssignee|TaskAssigneeChanged|
|MarkTaskUrgent|TaskMarkedUrgent + TaskUrgencyReasonRecorded|

\---

## 會議 5：ADR / Backlog 整理

### 目標

把事件風暴結果轉換成正式開發材料。

### 產出

* ADR
* Backlog Items
* Acceptance Criteria
* Spike
* Tech-base notes
* Devlog

例如：

```text
\[ADR] 使用 TaskStateChanged 搭配 WorkflowState.Category 表達任務狀態流轉
\[Feature] 建立 Task
\[Feature] 改變 Task State
\[Feature] 任務完成時產生 TaskCompleted
\[Spike] CompletedAt 應存在 Task Entity 或 Read Model
```

\---

# 四、下次會議建議規則

未來進行 RonFlow 事件風暴或類似討論時，可以採用以下規則。

## 規則 1：每回合只討論一個問題

例如：

```text
這回合只討論 TaskAssigned 是否是必經事件。
```

不要同時討論：

* assignee
* current action owner
* role-specific workflow
* 權限
* 報表
* workflow 設計

\---

## 規則 2：每回合最多產出四件事

```text
1. 一段對話摘要
2. 一張簡單 Mermaid 圖
3. 一個暫定結論
4. 一組 Open Questions / Parking Lot
```

\---

## 規則 3：未來功能直接放 Parking Lot

例如：

```text
子任務完成傳播 → Parking Lot
Regression task → Parking Lot
Role-specific workflow → Parking Lot
完整插單流程 → Parking Lot
```

先不要當場展開。

\---

## 規則 4：不在事件風暴中決定儲存細節

例如以下問題可以記錄，但不在事件風暴中解決：

```text
CompletedAt 要放 Task Entity 還是 Read Model？
TaskStateChanged 是否要存 snapshot？
WorkflowState.Category enum 名稱怎麼定？
```

這些應進入 ADR 或設計會議。

\---

## 規則 5：每三個回合就收斂一次

避免會議無限延伸。

例如：

```text
Round 1：討論主流程
Round 2：討論 Assignment
Round 3：討論 Done / Reopen
收斂一次
```

收斂內容包括：

* 已確認決策
* 暫定假設
* Open Questions
* Parking Lot
* 下一步

\---

# 五、對 RonFlow 個人開發的建議

因為 RonFlow 目前是一人專案，不需要模擬過長的完整會議。

更適合使用「短回合工作坊」：

```text
每次只問一個問題。
每次只產出一張圖或一張表。
每次最多 20～30 分鐘。
```

建議節奏：

```text
第 1 次：Task 的最小生命週期是什麼？
第 2 次：TaskStateChanged 是否取代固定狀態事件？
第 3 次：TaskCompleted 是否需要獨立事件？
第 4 次：TaskRejected 與 TaskReopened 的差異是什麼？
第 5 次：TaskAssigneeChanged 是否屬於主流程？
第 6 次：Priority 與 Urgent 如何區分？
```

這樣會更適合持續寫 devlog，也比較不會耗盡注意力。

\---

# 六、總結

這次事件風暴練習的內容很有價值，但節奏過度展開。

更準確地說，這次討論其實混合了：

```text
事件風暴
產品抽象
架構初探
流程設計
未來功能探索
```

如果是真實會議，這樣的密度會讓與會者疲勞，也會讓結論難以吸收。

比較好的方式是：

> 將事件風暴拆小，使用 timebox 控制節奏，透過 Parking Lot 保存重要但非本輪目標的議題，並把架構決策留到 ADR 或後續設計會議處理。

未來 RonFlow 的分析節奏可以改成：

```text
短回合
小輸出
明確收斂
嚴格控管範圍
會後整理成 Mermaid / ADR / Backlog
```

這樣既能保留深度，也能避免討論疲勞。

