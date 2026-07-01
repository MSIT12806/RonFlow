# RonFlow Hatchery 需求規格書

## 1. 文件定位

本文件是 RonFlow Hatchery 的 Software Requirements Specification，用來描述 RonFlow 應如何承接大型、抽象、尚未成形的工作，並將其逐步拆解為可進入既有 Flow 的可執行任務。

這份文件的目標是：

1. 固化訪談紀錄一到四中已確認的產品需求。
2. 作為後續 MVP Backlog、Gherkin Acceptance Criteria 與 SDD 初版的共同依據。
3. 說明 Hatchery 與既有 Project、Task、Flow、Subtask、Reminder、Template 的關係。
4. 描述可驗收的使用者行為、狀態規則、邊界情境與 MVP Definition of Done。

本文件描述的是需求與產品行為，不是資料表、API 或前端元件的最終設計。實作細節應在 SDD 中補齊。

## 2. 需求來源

本文件根據以下需求訪談紀錄整理：

1. `../requirements/RonFlow_Hatchery_訪談紀錄一_問題與使用情境收斂.md`
2. `../requirements/RonFlow_Hatchery_訪談紀錄二_任務成形模型.md`
3. `../requirements/RonFlow_Hatchery_訪談紀錄三_使用者流程與畫面.md`
4. `../requirements/RonFlow_Hatchery_訪談紀錄四_驗收_MVP切片與技術落地.md`

若本文件與後續 SDD、Backlog 或 Acceptance Criteria 不一致，應回到本文件確認產品需求，再判斷是否需要修訂 SRS。

## 3. 背景與問題

RonFlow 目前主要處理已經足夠明確、可以被執行與驗收的任務。使用者實際工作中會遇到大量尚未成形的大型任務，例如：

```text
設計一個分散式匯率查詢系統
規劃一個新產品功能
改善工程團隊產出衡量方式
建立一套會員系統
整理 RonFlow Hatchery 需求
```

這類任務通常不能直接放進既有 Flow，原因包括：

1. 完成定義不明確。
2. 任務仍需要拆解與釐清。
3. 任務可能包含多層子任務。
4. 若直接進入 Active，會長期卡住並污染 Kanban 視野。
5. 使用者目前被迫先用 Markdown 或其他外部工具整理，再把已成形的任務搬進 RonFlow。

Hatchery 要解決的核心問題是：

```text
RonFlow 不只要管理任務如何被完成，也要管理任務如何從模糊變清楚。
```

## 4. 核心價值主張

RonFlow Hatchery 的核心價值主張是：

```text
使用者可以把大型任務直接放進 RonFlow，在任務樹中持續拆解，直到葉任務足夠明確，再送進既有 Flow 執行。
```

當 Hatchery 做對時，使用者應能：

1. 不再先用 Markdown 當前置任務整理區。
2. 在 RonFlow 中直接建立大型、抽象、尚未規劃完成的工作。
3. 將任務遞迴拆成父任務與葉任務。
4. 為葉任務補齊完成條件與預估耗時。
5. 將已 Ready 的葉任務送進既有 Flow。
6. 讓 Flow 完成狀態同步回任務樹。
7. 讓父任務保留為統計與進度節點。

## 5. 目標與非目標

### 5.1 目標

Hatchery MVP 應達成以下目標：

1. Project 底下可以承載任務樹。
2. 任務可以建立 child task，形成不限層數的遞迴結構。
3. 新任務預設為葉任務。
4. 葉任務可以填寫 Ready 欄位。
5. 葉任務符合 Ready 條件後，可以送進既有 Flow。
6. 建立 child task 後，原任務轉為父任務。
7. 父任務不能直接進 Flow。
8. 父任務可以手動標記拆解完成。
9. 父任務可以顯示直接子任務完成進度。
10. 所有直接子任務完成後，父任務自動完成。
11. Flow 中任務完成後，同一筆任務在任務樹中同步完成。
12. 已進 Flow 的葉任務可以退回 Hatchery。
13. 已進 Flow 的任務新增 child task 後，退出 Flow 並轉為父任務。

### 5.2 非目標

Hatchery MVP 不處理以下功能：

1. AI 自動拆解。
2. AI Ready 判讀。
3. AI 自動估時。
4. Mind Map 視覺化。
5. 多種 view。
6. Markdown 匯入。
7. Dashboard 或完整報表。
8. 任務類型分類。
9. 標籤功能。
10. 不 Ready 原因管理。
11. 複雜依賴或 lock 機制。
12. 拖拉改層級。
13. 跨專案依賴。
14. 多人協作權限細節。
15. 完整狀態異動紀錄。
16. 完成特效。
17. Project subtask template 與 Ready list 的整合。

## 6. 核心名詞

### 6.1 Project

Project 是 RonFlow 中的工作區域與容器。Project 不是任務，不是超大型任務，也不進入 Flow。

### 6.2 Task

Task 是 RonFlow 中主要工作物件。Hatchery 中不額外引入 Epic、Goal、Work Package 等名稱，介面上仍統一稱為任務。

### 6.3 Task Tree

Task Tree 是 Project 底下的任務拆解結構。Task Tree 支援任務與任務之間的 parent-child 關係，且不限制層數。

```text
Project
  └── Task Tree
        ├── Task
        │     ├── Task
        │     └── Leaf Task
        └── Leaf Task
```

### 6.4 Parent Task

Parent Task 是擁有 child task 的任務。父任務負責承載拆解結構、顯示子任務進度、保留大型任務的生命週期，不直接進入 Flow。

### 6.5 Leaf Task

Leaf Task 是沒有 child task 的任務。新建立的任務預設為葉任務。葉任務可以填寫 Ready 欄位，符合條件後送進 Flow。

### 6.6 Child Task

Child Task 是另一個 Task 底下的子任務節點。Child task 是正式任務，可以獨立完成、可以進 Flow，也可以再建立自己的 child task。

### 6.7 Subtask List

Subtask list 是既有 Task 底下的 checklist item，概念上不是獨立任務節點。它可能有排序意義，也可能由 Project subtask template 在建立 Task 時產生。

Hatchery MVP 中，child task 與 subtask list 不是同一個東西。

### 6.8 Ready List

Ready list 是葉任務進入 Flow 前需補齊的最低欄位集合。MVP Ready list 包含完成條件與預估耗時。

### 6.9 Flow

Flow 是 RonFlow 既有任務執行流程，例如：

```text
Todo -> Active -> Review -> Done
```

Hatchery 負責任務成形，Flow 負責已 Ready 葉任務的執行與完成。

### 6.10 拆解完成

拆解完成是父任務的手動標記，表示使用者認為該父任務目前已拆到可接受程度。拆解完成不等於任務完成。

## 7. 產品模型

### 7.1 Hatchery 與 Flow 的分工

Hatchery 不取代 Flow。

```text
Hatchery：負責任務拆解、成形、Ready 判斷。
Flow：負責已 Ready 任務的實際推進與驗收。
```

使用者在 Hatchery 階段主要關心：

1. 任務是否還需要拆解。
2. 任務是否為父任務或葉任務。
3. 葉任務是否符合 Ready 條件。
4. 父任務是否已拆解完成。
5. 子任務是否完成。

使用者在 Flow 階段主要關心：

1. 任務目前在 Todo、Active、Review 或 Done。
2. 任務是否可被編輯、完成、退回或刪除。

### 7.2 同一筆任務跨 Hatchery 與 Flow

Ready 葉任務送進 Flow 後，不建立副本、不從任務樹移除。它仍是同一筆 Task，只是開始具有 Flow 呈現與 Flow 狀態。

這個規則用來避免：

1. 任務樹與 Flow 資料重複。
2. 父任務無法統計子任務完成。
3. 使用者無法追蹤任務從成形到完成的完整歷程。

### 7.3 任務類型由結構決定

MVP 中不需要額外儲存使用者可見的任務分類。

```text
沒有 child task = Leaf Task
有 child task = Parent Task
```

任務從葉任務變成父任務時，依據是否新增 child task 判斷。

## 8. MVP 主流程

Hatchery MVP 的主流程如下：

1. 使用者進入 Project 的任務樹工作區。
2. 使用者建立任務。
3. 新任務預設為葉任務。
4. 使用者可為葉任務填寫完成條件與預估耗時。
5. 欄位填完整後，葉任務可送進 Flow。
6. 使用者也可以選擇建立 child task。
7. 一旦建立 child task，原任務轉為父任務。
8. 父任務不能進 Flow。
9. 父任務顯示 child task list。
10. 葉任務進 Flow 後，Flow 完成會同步回任務樹。
11. 所有直接子任務完成後，父任務自動完成。

## 9. 使用者介面需求

### 9.1 Project 工作區

MVP 應在 Project 工作區中支援任務樹與既有 Flow 的共同操作。

第一代畫面方向為：

```text
任務樹 / Hatchery 區域
Flow / Kanban 區域
```

兩者可以採並排顯示。具體版面由後續 UI 設計決定，但產品需求是：

1. 大型任務與未 Ready 任務不直接污染 Flow。
2. 使用者仍能在同一個 Project 脈絡中看見任務樹與 Flow 任務。
3. 任務樹上的子任務應能一目瞭然。

### 9.2 任務建立

新增任務時，MVP 最小輸入欄位為：

```text
任務名稱
```

新任務建立後預設為葉任務，並可在 detail modal 中編輯 Ready 欄位。

### 9.3 Task Detail Modal

MVP 沿用既有 Task Detail Modal，不新增獨立任務詳細頁，不改成 drawer。

葉任務 detail modal 應支援：

1. 建立 child task。
2. 編輯完成條件。
3. 編輯預估耗時。
4. 送進 Flow。

父任務 detail modal 應支援：

1. 建立 child task。
2. 顯示 child task list。
3. 開啟 child task 的 detail modal。
4. 手動標記拆解完成。
5. 顯示直接子任務完成進度。

### 9.4 送進 Flow 按鈕

葉任務未符合 Ready 條件時，仍可顯示「送進 Flow」按鈕，但按鈕應為 disabled。

當葉任務符合 Ready 條件後，「送進 Flow」按鈕啟用。

父任務不顯示可用的「送進 Flow」操作。

## 10. Ready 條件

### 10.1 MVP Ready 條件

MVP 中，葉任務符合 Ready 的條件為：

1. 至少一筆完成條件。
2. 有填預估耗時。

原本獨立的「產出物」欄位不納入 MVP。若任務有產出物，使用者應寫入完成條件。

### 10.2 完成條件

完成條件允許多筆。

例如：

```text
完成條件：
- 完成任務樹頁面基本顯示
- 可建立 child task
- 父任務可顯示 3 / 10 進度
```

完成條件不應只是一段純文字，應支援多筆項目。

### 10.3 預估耗時

預估耗時是工時，不是到期時間。

MVP 支援單位：

```text
分鐘
小時
天
```

預估耗時採數字加單位，例如：

```text
30 分鐘
2 小時
1 天
```

Reminder 可在後續版本整合，但 MVP 中預估耗時不直接等於 reminder 或 due date。

### 10.4 Ready 判斷方式

MVP 不採 AI 判讀、不採自動複雜度估算、不處理不 Ready 原因。

系統只依明確欄位判斷：

```text
完成條件有至少一筆
且
預估耗時有值
```

## 11. 父任務規則

### 11.1 父任務轉換

當葉任務新增 child task 時，該任務轉為父任務。

若葉任務已填寫 Ready 欄位：

1. Ready 狀態失效。
2. Ready 欄位資料不清空。
3. Ready 欄位不再作為主要操作顯示。
4. 保留資料以支援未來反悔或恢復情境。

### 11.2 父任務不能進 Flow

父任務不能直接送進 Flow。若父任務本身有可交付產出，使用者應另拆出一個葉任務承擔該產出。

### 11.3 拆解完成

父任務可以手動標記拆解完成。

拆解完成表示：

```text
此父任務目前已拆到可接受程度。
```

拆解完成不表示：

```text
此任務已完成。
此任務底下所有子任務已完成。
```

MVP 中，父任務已標記拆解完成後仍可新增 child task。新增 child task 後，MVP 不自動取消拆解完成、不提示、不做狀態回退。

### 11.4 父任務進度

父任務進度顯示格式為：

```text
完成直接子任務數 / 直接子任務總數
```

例如：

```text
3 / 10
```

MVP 不以所有 descendant 計算父任務進度。

### 11.5 父任務自動完成

父任務完成判定採直接子任務規則：

```text
所有直接子任務完成後，父任務自動完成。
```

若父任務底下還有另一個父任務，則該子父任務需先依自己的直接子任務完成規則完成。整體完成會形成遞迴結果，但每個父任務只判斷自己的直接子任務。

父任務自動完成時，MVP 不需要跳出通知。

## 12. Flow 銜接規則

### 12.1 送進 Flow

符合 Ready 條件的葉任務可以送進 Flow。

MVP 暫定送進 Flow 後進入 Todo。若既有 RonFlow 固定流程已有不同 initial state 機制，實作時應以既有 Flow 機制為準，但 SDD 需明確說明。

### 12.2 Flow 完成同步

同一筆任務在 Flow 中完成後，任務樹中的該任務也視為完成。

當父任務底下所有直接子任務完成後，父任務自動完成。

### 12.3 已進 Flow 任務退回 Hatchery

已進 Flow 的葉任務允許退回 Hatchery。

退回後：

1. 任務不再顯示於 Flow。
2. 任務仍保留於任務樹。
3. 任務可以繼續編輯 Ready 欄位或建立 child task。

### 12.4 已進 Flow 任務新增 child task

已進 Flow 的葉任務若新增 child task：

1. 該任務轉為父任務。
2. Ready 狀態失效。
3. 該任務退出 Flow。
4. 該任務回到 Hatchery / 任務樹中。

此規則也適用於既有 Flow task。也就是既有 Flow task 可以被重新拆解為父任務。

### 12.5 Flow 編輯

Ready 任務送進 Flow 後，仍可在 Flow 中編輯完成條件。

使用者視角為：

```text
未進 Flow：在任務樹 / Hatchery 中編輯。
已進 Flow：在 Flow 中編輯。
```

## 13. 刪除與邊界情境

### 13.1 刪除父任務

MVP 中，刪除父任務時，一併刪除所有 child task。

MVP 不要求刪除確認視窗。

此規則存在資料風險，後續 SDD 或 UX 可標註為風險點，並評估是否需要確認 modal、顯示子任務數量、封存或復原機制。

### 13.2 刪除已進 Flow 葉任務

任務樹中的任務與 Flow 中的任務是同一筆資料。刪除已進 Flow 的葉任務時，應同步刪除它在 Flow 中的呈現。

### 13.3 Ready 任務新增 child task

Ready 葉任務新增 child task 時，應要求使用者確認其將轉為父任務。確認後：

1. 原任務轉為父任務。
2. Ready 狀態失效。
3. Ready 欄位資料保留。

### 13.4 子任務排序與依賴

Child task 在 MVP 中不代表執行順序。若未來需要表達先後依賴，應另行設計 lock 或 dependency 機制。

MVP 不支援拖拉改層級。

## 14. 既有系統相容性

### 14.1 Task 與 Subtask

現有 RonFlow 中：

```text
Task 是主要工作項目。
Subtask 是 Task 底下的 checklist item。
Subtask 不是獨立任務節點。
```

Hatchery 新增的 child task 是 Task 與 Task 之間的 parent-child 關係。

### 14.2 Task Status

現有 Flow 狀態存在 task 本身：

```text
task.status = Todo / Active / Review / Done
```

Hatchery 需要支援未進 Flow 的任務。未進 Flow 任務如何表示、Flow status 與 Hatchery 狀態是否共用欄位，需在 SDD 中決定。

### 14.3 Reminder

Reminder 掛在 task 上。

MVP 中預估耗時不直接整合 reminder。未來若需要整合，Task 可作為共同關聯點。

### 14.4 Project Subtask Template

現有 Project subtask template 會在建立 task 時自動產生 subtask checklist。

MVP 中 Ready list 手動輸入，不整合 Project subtask template。未來可再評估是否讓 Ready list 套用 template 或與既有 subtask list 整合。

### 14.5 權限與多人協作

MVP 不處理新的多人協作權限規則。

但資料模型與操作設計不應排除未來 owner、user、permission 或 audit 擴充。

## 15. MVP 功能切片順序

MVP backlog 應以操作型功能優先：

1. 建立任務樹資料結構與 child task 操作。
2. 在任務 detail modal 中建立 child task。
3. 葉任務與父任務切換規則。
4. 葉任務 Ready list：完成條件與預估耗時。
5. Ready 葉任務送進 Flow。
6. Flow 任務完成後同步任務樹狀態。
7. 已進 Flow 任務退回 Hatchery，或新增 child task 後退出 Flow。
8. 任務樹頁面顯示與父任務進度。

操作型優先原則：

```text
先做能改變資料狀態的操作。
再做支援查詢與視覺呈現的頁面。
```

## 16. 主驗收案例

主驗收案例使用 RonFlow 自己的 Hatchery 需求：

```text
RonFlow 需求說明書：抽象任務分解與任務成形管理
```

驗收流程：

1. 使用者在 Project 中建立「RonFlow Hatchery」或「整理 RonFlow Hatchery 需求」大型任務。
2. 系統預設它是葉任務，顯示 Ready 欄位。
3. 使用者發現它太大，建立 child task。
4. 原任務轉為父任務，Ready 欄位不再作為主要操作。
5. 使用者拆出訪談、規格書、SDD、Backlog 等 child task。
6. 某些 child task 可以繼續拆解。
7. 使用者為「撰寫 SRS」這類葉任務填寫至少一筆完成條件與預估耗時。
8. 系統啟用「送進 Flow」操作。
9. 使用者將該葉任務送進 Flow。
10. 任務進入 Todo 或既有 Flow initial state。
11. Flow 中完成該任務後，任務樹中的同一筆葉任務同步完成。
12. 父任務進度同步更新。
13. 所有直接子任務完成後，父任務自動完成。

## 17. MVP Definition of Done

Hatchery MVP 完成定義如下：

```text
當使用者能在 Project 中建立大型任務，
將其遞迴拆成父任務與葉任務，
為葉任務填寫完成條件與預估耗時，
將葉任務送進 Flow，
並在 Flow 完成後同步更新任務樹與父任務進度，
即視為 Hatchery MVP 完成。
```

## 18. 後續版本議題

以下議題不納入 MVP，但應保留為後續版本候選：

1. Ready list 與既有 subtask list 整合。
2. Project subtask template 套用到 Ready list。
3. 任務樹拖拉改層級。
4. 任務依賴或 lock 機制。
5. Markdown 大綱匯入。
6. Mind Map 或多種視覺化 view。
7. 父任務進度改採所有 descendant 或葉任務口徑。
8. 拆解完成後新增 child task 的提示與狀態紀錄。
9. 刪除父任務確認與復原機制。
10. 已進 Flow 任務新增 child task 的提示。
11. 完整狀態異動紀錄。
12. Dashboard、報表與統計。
13. 完成動畫或其他正向回饋。
14. 多人協作權限與 audit 擴充。
15. AI 輔助拆解、Ready 判讀或估時。

## 19. SDD 待設計事項

後續 SDD 至少需要回答以下問題：

1. Task parentId 如何設計。
2. 未進 Flow 任務的 status 如何表示。
3. Flow status 與 Hatchery 狀態是否共用欄位。
4. Ready list 使用新表、既有 subtask list，或 Task 關聯欄位。
5. 完成條件多筆資料如何儲存。
6. 預估耗時欄位如何儲存數字與單位。
7. 已進 Flow 任務退回 Hatchery 的狀態轉換。
8. 已進 Flow 任務新增 child task 後如何退出 Flow。
9. 父任務自動完成的交易與遞迴更新方式。
10. 刪除父任務時 cascade delete 的資料一致性。
11. 既有 Flow task 轉父任務時的相容性。
12. Project subtask template 與 Ready list 的未來整合路徑。
13. 任務樹與 Flow 並排畫面的最小實作方式。
14. 相關 API、E2E、integration test 與 unit test 覆蓋範圍。

