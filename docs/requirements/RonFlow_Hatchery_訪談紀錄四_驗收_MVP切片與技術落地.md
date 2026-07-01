# RonFlow Hatchery 訪談紀錄四：驗收、MVP 切片與技術落地

## 一、訪談目的

本次訪談為 Hatchery 規劃中的第四次訪談，主題是「驗收、MVP 切片與技術落地」。

前三次訪談已完成：

```text
訪談一：問題與使用情境收斂
訪談二：任務成形模型
訪談三：使用者流程與畫面
```

第四次訪談的目標，是把前面三次收斂出的需求，轉換成可以開始落地的開發方向，包括：

```text
1. 確認 MVP 主流程。
2. 確認 MVP 功能切片方向。
3. 定義 Ready 條件。
4. 定義父任務完成與進度規則。
5. 釐清既有 subtask / template / flow / reminder 的相容性。
6. 補齊刪除、退回、修改等邊界情境。
7. 補齊 SDD 所需的現有系統資訊。
8. 確認主驗收案例與 Definition of Done。
9. 決定後續正式文件產出順序。
```

---

## 二、訪談後結論摘要

第四次訪談完成後，可以確認：

```text
RonFlow Hatchery 已經具備撰寫需求規格書與 MVP backlog 的條件。
```

也可以開始進入 SDD，但 SDD 需要依據現有 RonFlow 技術現況補上較細的設計，例如：

```text
Task 資料模型
Subtask checklist 模型
Task status / Flow status 設計
Reminder 設計
Project subtask template 套用機制
權限與協作預留策略
```

本次訪談已補到足以撰寫 SDD 初版的關鍵資訊。

---

## 三、MVP 核心流程確認

### Q71 結論

MVP 的核心流程確認如下：

```text
1. 使用者在 Project 任務樹中建立任務。
2. 新任務預設為葉任務。
3. 使用者可填寫完成條件與預估耗時。
4. 欄位填完整後，葉任務可送進 Flow。
5. 使用者也可以選擇建立子任務。
6. 一旦建立子任務，原任務轉為父任務。
7. 父任務不能進 Flow。
8. 父任務顯示 child task list。
9. 葉任務進 Flow 後，Flow 完成會同步回任務樹。
10. 所有子任務完成後，父任務自動完成。
```

使用者確認：

```text
這條就是 MVP 主流程。
```

---

### Q72 結論

MVP 的核心價值主張確認為：

```text
使用者可以把大型任務直接放進 RonFlow，在任務樹中持續拆解，直到葉任務足夠明確，再送進既有 Flow 執行。
```

此句可作為需求規格書的核心價值主張。

---

## 四、MVP 功能切片方向

### Q73～Q78 結論

原先提出的切片包含：

```text
Project 任務樹頁面
建立 child task
葉任務 Ready 欄位
Ready 葉任務送進 Flow
Flow 完成同步回任務樹
已進 Flow 任務新增子任務後退出 Flow
```

使用者提醒：

```text
切片應該以操作型功能優先。
查詢功能需要等操作型功能實作完後，再接著做。
```

因此，MVP backlog 不應先以「查詢畫面」或「展示功能」為優先，而應先確保核心操作可完成。

### 調整後的功能切片順序

建議 MVP 切片順序調整為：

```text
1. 建立任務樹資料結構與 child task 操作。
2. 在任務 detail modal 中建立 child task。
3. 葉任務與父任務切換規則。
4. 葉任務 Ready list：完成條件與預估耗時。
5. Ready 葉任務送進 Flow。
6. Flow 任務完成後同步任務樹狀態。
7. 已進 Flow 任務退回 Hatchery / 新增 child task 後退出 Flow。
8. 任務樹頁面顯示與父任務進度。
```

### 操作型優先原則

後續 backlog 應遵守：

```text
先做能改變資料狀態的操作。
再做支援查詢與視覺呈現的頁面。
```

例如：

```text
先支援建立 child task，再做任務樹完整展示。
先支援 Ready 欄位與送進 Flow，再做 Ready 任務篩選。
先支援 Flow Done 同步，再做父任務進度顯示。
```

---

## 五、Ready 條件與驗收規則

### Q79 結論：Ready 條件

MVP Ready 條件確認為：

```text
1. 至少一筆完成條件。
2. 有填預估耗時。
```

原本獨立的「產出物」欄位正式移除。

若任務有產出物，應寫入完成條件中。

---

### Q80 結論：完成條件允許多筆

完成條件允許多筆。

例如：

```text
完成條件：
- 完成任務樹頁面基本顯示
- 可建立 child task
- 父任務可顯示 3 / 10 進度
```

這表示完成條件在資料結構上不應只是一段純文字，而應支援多筆 checklist item。

---

### Q81 結論：預估耗時單位

MVP 預估耗時單位支援：

```text
分鐘
小時
天
```

預估耗時採：

```text
數字 + 單位
```

例如：

```text
30 分鐘
2 小時
1 天
```

---

### Q82 結論：預估耗時是工時，不是到期時間

MVP 中的預估耗時定義為：

```text
工時
```

不是 deadline / due date。

也就是：

```text
預估耗時：預估要花多少時間完成
到期時間：必須在某個日期前完成
```

兩者概念分開。

Reminder 可以後續整合，但 MVP 的預估耗時不直接等同到期時間。

---

### Q83 結論：未填完整 Ready 欄位時的畫面阻擋

使用者選擇：

```text
顯示「送進 Flow」按鈕，但按鈕 disabled。
```

因此：

```text
完成條件未填，送進 Flow disabled。
預估耗時未填，送進 Flow disabled。
兩者都填寫後，送進 Flow enabled。
```

---

## 六、父任務規則

### Q84 結論：父任務完成進度計算直接子任務

父任務進度例如：

```text
3 / 10
```

MVP 計算：

```text
直接子任務完成數 / 直接子任務總數
```

不計算所有 descendant。

---

### Q85 結論：父任務自動完成看直接子任務

父任務完成判定採：

```text
所有直接子任務完成後，父任務自動完成。
```

例如：

```text
父任務 A
  ├── 子任務 B
  │    ├── 葉任務 B1
  │    └── 葉任務 B2
  └── 葉任務 C
```

A 是否完成，只看：

```text
B 是否完成
C 是否完成
```

而 B 是否完成，則由 B 自己底下的直接子任務 B1、B2 決定。

這形成遞迴完成規則：

```text
每個父任務只負責判斷自己的直接子任務。
```

---

### Q86 結論：MVP 要做拆解完成

父任務「拆解完成」要納入 MVP。

規則：

```text
父任務可以手動標記拆解完成。
拆解完成不等於任務完成。
任務完成仍由所有直接子任務完成後自動判定。
```

---

### Q87 結論：新增子任務後，不自動取消拆解完成

若父任務已標記拆解完成，後續又新增子任務：

```text
MVP 暫時不處理。
```

也就是：

```text
不自動取消拆解完成。
不提示。
不做狀態回退。
```

此行為未來可再補狀態紀錄或提醒。

---

## 七、既有 Subtask / Template 相容性

### Q88 結論：Subtask list 與 child task 不是同一個東西

使用者同意以下區分：

```text
subtask list：比較像某個 task 的完成檢核項目，可能有順序。
child task：真正的任務節點，可獨立完成、可進 Flow、可再拆。
```

但使用者補充：

```text
subtask 很可能是 leaf task 才會有的東西。
subtask 概念上會跟 ready list 重疊。
兩者都是檢核項目。
```

因此，後續要觀察：

```text
是否需要讓 ready list 可以直接帶入 subtask template 設定。
```

---

### Q89 結論：Ready list 先獨立做，不急著整合 subtask list

使用者選擇：

```text
MVP 先做新的完成條件 / ready list。
之後若確認 ready list 與 subtask list 功能重疊，再進行整合。
```

因此 MVP 不直接改造既有 subtask list。

---

### Q90 結論：Project subtask template MVP 先不整合

使用者選擇：

```text
Project subtask template 先不要整合，第二版再說。
```

因此 MVP 中：

```text
Ready list 手動輸入。
不自動套用 project subtask template。
```

---

### Q91 結論：舊有 Flow task 可以轉成父任務

MVP 允許：

```text
已存在 Flow 的 task
  → 新增 child task
  → 退出 Flow
  → 轉成父任務
```

這表示 Hatchery 不只支援新建立的任務，也要能處理既有 Flow task 被重新拆解的情境。

---

## 八、刪除、退出、回復等邊界情境

### Q92 結論：刪除父任務不需要確認視窗

使用者選擇：

```text
不需要確認視窗，直接刪。
```

因此 MVP 暫定：

```text
刪除父任務時，一併刪除所有子任務。
不額外顯示確認視窗。
```

不過此處有潛在資料風險，後續 SDD / UX 可標註為風險點。

---

### Q93 結論：刪除已進 Flow 的葉任務時，同步刪除

使用者選擇：

```text
同步刪除。
```

並補充：

```text
它們應該是同一個資料。
```

因此：

```text
任務樹中的任務與 Flow 中的任務是同一筆資料。
刪除該任務，即刪除它在 Flow 中的呈現。
```

---

### Q94 結論：葉任務送進 Flow 後，允許退回 Hatchery

使用者選擇：

```text
允許退回，退回後離開 Flow。
```

因此 MVP 支援：

```text
已進 Flow 的葉任務可以退回 Hatchery。
退回後不再顯示於 Flow。
仍保留於任務樹。
```

---

### Q95 結論：Ready 任務送進 Flow 後，完成條件仍可在 Flow 編輯

使用者選擇：

```text
可以在 Flow 編輯完成條件。
```

因此，進 Flow 後任務仍可編輯 Ready list / 完成條件。

這與第三次訪談「已進 Flow 後去 Flow 編輯」一致。

---

## 九、資料與技術落地前置資訊

以下為 SDD 初版的重要技術前提。

### Q96 結論：現有 Task 與 Subtask 關係

使用者回答：

```text
Task 有 Subtask list，subtask 只是 checklist item。
```

因此目前資料模型可假設：

```text
Task 是主要工作項目。
Subtask 是 Task 底下的 checklist item。
Subtask 不是獨立任務節點。
```

新的 child task 則會是：

```text
Task 與 Task 之間的 parent-child 關係。
```

---

### Q97 結論：現有 Flow 狀態存在 task 本身

使用者回答：

```text
task.status = Todo / Active / Review / Done
```

因此現有 Flow 狀態可視為 Task 的 status 欄位。

MVP 若引入 Hatchery，需考慮：

```text
Task 同時需要支援 Flow status 與任務樹 parentId。
未進 Flow 的任務狀態如何表示。
已進 Flow 的任務使用既有 status。
```

---

### Q98 結論：Reminder 掛在 task 上

使用者回答：

```text
Reminder 掛在 task 上。
```

因此：

```text
預估耗時與 reminder 目前不直接整合。
若未來要整合，可由 Task 作為共同關聯點。
```

---

### Q99 結論：Project subtask template 會在建立 task 時自動產生 subtask checklist

使用者回答：

```text
建立 task 時自動產生 subtask checklist。
```

因此現有機制為：

```text
Project template
  → 建立 task 時
  → 自動產生 subtask checklist
```

MVP Ready list 暫不整合此機制，但未來可能需處理重疊。

---

### Q100 結論：未來可能多人協作，但 MVP 不處理權限

使用者選擇：

```text
未來可能多人，但 MVP 不處理權限。
```

因此 SDD 應採：

```text
MVP 不做多人協作功能。
但資料模型可以避免完全排除未來 owner / user / permission 擴充。
```

---

## 十、驗收案例與 Definition of Done

### Q101 結論：主驗收案例

主驗收案例仍使用：

```text
RonFlow 需求說明書：抽象任務分解與任務成形管理
```

也就是使用 RonFlow 自己的 Hatchery 需求來驗證 Hatchery 功能。

---

### Q102 結論：MVP 至少要完成的真實操作

使用者選擇：

```text
E. 以下都要。
```

包含：

```text
A. 把 RonFlow Hatchery 需求建立成任務樹。
B. 拆出訪談、規格書、SDD、Backlog 等子任務。
C. 把「撰寫 SRS」這種葉任務送進 Flow。
D. Flow 完成後，父任務進度同步更新。
```

---

### Q103 結論：MVP Definition of Done

使用者確認以下段落可以放進規格書：

```text
當使用者能在 Project 中建立大型任務，
將其遞迴拆成父任務與葉任務，
為葉任務填寫完成條件與預估耗時，
將葉任務送進 Flow，
並在 Flow 完成後同步更新任務樹與父任務進度，
即視為 Hatchery MVP 完成。
```

---

## 十一、後續正式文件產出順序

### Q104 結論

使用者選擇：

```text
全部依序產出。
```

建議順序：

```text
1. RonFlow Hatchery 需求規格書 / SRS
2. MVP Backlog
3. Gherkin Acceptance Criteria
4. SDD 初版
```

其中建議先從 SRS 開始。

原因：

```text
SRS 會成為後續 SDD、Backlog 與 Acceptance Criteria 的共同依據。
```

---

## 十二、第四次訪談後的整體結論

第四次訪談完成後，RonFlow Hatchery 已具備正式落地條件。

### 可以開始撰寫的文件

```text
1. RonFlow Hatchery 需求規格書 / SRS
2. MVP 功能 Backlog
3. Gherkin Acceptance Criteria
4. SDD 初版
```

### 需求已明確的部分

```text
1. MVP 主流程
2. Ready 條件
3. 父任務與葉任務規則
4. 任務進 Flow 條件
5. Flow 完成同步規則
6. 已進 Flow 任務退回 Hatchery 規則
7. 既有 task / subtask / status / reminder / template 的基本現況
8. MVP Definition of Done
```

### 仍需在 SDD 或實作前細化的部分

```text
1. Task parentId 如何設計。
2. 未進 Flow 任務的 status 如何表示。
3. Flow status 與 Hatchery 狀態是否共用欄位。
4. Ready list 是否使用新表，或暫存在 Task 相關欄位。
5. 完成條件多筆資料如何儲存。
6. 預估耗時欄位如何設計。
7. 已進 Flow 任務退回 Hatchery 的狀態轉換。
8. 已進 Flow 任務新增 child task 後如何退出 Flow。
9. 父任務自動完成的交易與遞迴更新方式。
10. 舊 task 自動帶入 project subtask template 與新 ready list 的相容問題。
```

---

## 十三、建議的下一步

建議下一步直接產出：

```text
RonFlow Hatchery 需求規格書 / SRS
```

該文件應包含：

```text
1. 背景與問題
2. 目標與非目標
3. 使用者與使用情境
4. 核心名詞
5. 功能需求
6. 狀態規則
7. Ready 條件
8. Flow 銜接規則
9. 邊界條件
10. MVP Definition of Done
11. 後續版本議題
```

SRS 完成後，再依序產出：

```text
MVP Backlog
Gherkin Acceptance Criteria
SDD 初版
```
