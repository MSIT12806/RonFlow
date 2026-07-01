# RonFlow Hatchery 訪談紀錄三：使用者流程與畫面

## 一、訪談目的

本次訪談為 Hatchery 規劃中的第三次訪談，主題是「使用者流程與畫面」。

前兩次訪談已確認：

```text
Project 是工作區域，不是任務。
Hatchery / 任務樹負責任務成形。
Flow 負責已 Ready 的葉任務執行。
新任務預設為葉任務。
建立子任務後，原任務轉為父任務。
父任務不能進 Flow。
葉任務通過 Ready checklist 後才能進 Flow。
```

第三次訪談的重點，是確認使用者實際如何在 RonFlow 中操作這套模型，包括：

```text
1. 使用者如何看見大型任務與 Flow 任務。
2. 使用者如何建立任務、拆解子任務。
3. 任務 detail modal 要新增哪些欄位與操作。
4. Ready checklist 如何填寫與驗證。
5. 葉任務如何送進 Flow。
6. Flow 狀態如何同步回任務樹。
7. 父任務進度與完成狀態如何呈現。
8. MVP 的畫面範圍與非目標。
```

---

## 二、前置共識

### 1. RonFlow 既有設計

目前 RonFlow 已有一個 Board / Kanban，用來看見所有 Flow 裡面的任務。

既有 Flow 任務大致會在看板中呈現，例如：

```text
Todo → Active → Review → Done
```

目前也已有 Task Detail Modal，用來編輯任務細節。

### 2. Hatchery 新需求

大型任務與尚未 Ready 的任務，不適合直接放入 Flow。

原因是：

```text
1. 大型任務完成定義不明。
2. 大型任務可能需要持續拆解。
3. 若放進 Flow，容易長期卡住。
4. 會污染既有 Kanban，使執行中任務不再清楚。
```

因此，第三次訪談的核心 UI 問題是：

```text
如何在同一個操作窗口中，同時看見任務樹與 Flow 任務，
又不把尚未 Ready 的大型任務直接放入 Flow？
```

---

## 三、主要畫面方向

### 1. 第一代採「任務樹與 Flow 並排顯示」

使用者希望所有任務都能一目瞭然。

目前初步方向為：

```text
第一代先採並排顯示設計。
```

也就是在同一個 Project 工作區中，同時呈現：

```text
左側或一側：任務樹 / Hatchery 區域
右側或另一側：既有 Flow / Kanban 區域
```

概念上：

```text
Project
  ├── 任務樹區：大型任務、父任務、葉任務、尚未 Ready 任務
  └── Flow 區：已 Ready 並送入 Flow 的葉任務
```

### 2. 大型任務不進 Flow，但要能在同一窗口看見

使用者明確表示：

```text
大型任務很明顯不能放到 Flow 裡面。
```

但同時也希望：

```text
所有任務能一目瞭然。
```

因此，Hatchery 不應該是完全獨立、與 Flow 割裂的另一套介面，而應該和既有 Flow 共同存在於 Project 的主要工作視圖中。

### 3. 可能的呈現方式

使用者提到可能的方向：

```text
1. 手風琴方式呈現大型任務與其子孫任務。
2. 並排顯示任務樹與 Flow。
3. 未來可設計多種不同呈現方式，滿足不同需求。
```

但 MVP 先採：

```text
並排顯示。
```

---

## 四、任務建立流程

### 1. 新增任務的預設位置

使用者選擇：

```text
如果沒有通過 Ready checklist，就留在任務樹；
通過後才可進 Flow。
```

因此，新任務預設不直接進入 Flow，而是先建立在任務樹中。

### 2. 新任務的最小輸入欄位

使用者選擇：

```text
建立時只填名稱，進入 Ready checklist 時再補其他欄位。
```

因此，新增任務時的最小欄位為：

```text
任務名稱
```

任務建立後，使用者可以：

```text
1. 進入 task detail modal 編輯 Ready checklist。
2. 或選擇建立子任務，使該任務轉為父任務。
```

### 3. 新任務預設為葉任務

延續第二次訪談結論：

```text
新建立任務預設為葉任務。
```

葉任務應具備 Ready checklist。

但如果使用者點擊「建立子任務」，則原任務會轉為父任務。

---

## 五、Task Detail Modal 設計

### 1. 沿用既有 Detail Modal

目前 RonFlow 已有任務 detail modal。

第三次訪談確認：

```text
MVP 繼續使用既有 detail modal。
不新增獨立任務詳細頁。
不改成 drawer。
```

### 2. 葉任務 Detail Modal 需要新增的項目

使用者認為葉任務 detail modal 需要新增：

```text
1. 建立子任務按鈕
2. Ready checklist
3. 完成條件
4. 預估耗時
5. 送進 Flow 按鈕
```

其中，Ready checklist 可能可以取代原本的 subtask list。

### 3. 「產出物」欄位移除

原本 Ready checklist 暫定為：

```text
1. 明確產出物
2. 完成條件
3. 預估完成時間
```

本次訪談調整為：

```text
拿掉「產出物」欄位。
如果有產出物，寫在「完成條件」中即可。
```

因此，MVP Ready checklist 改為：

```text
1. 完成條件
2. 預估耗時
```

其中完成條件可包含產出物描述。

### 4. 完成條件欄位形式

使用者希望完成條件欄位類似：

```text
完成條件：
[請填入文字] [新增 btn]
[之前填的東西]
```

也就是完成條件可能是一組條列項目，而不是單一文字欄位。

例如：

```text
完成條件：
- 定義父任務與葉任務狀態
- 定義 Ready checklist 欄位
- 說明葉任務送入 Flow 的規則
```

### 5. 預估耗時欄位形式

使用者希望預估耗時採：

```text
[數字] [單位]
```

例如：

```text
2 小時
0.5 天
3 天
```

這延續第二次訪談中「數字 + 單位」的結論。

### 6. Ready checklist 與 Project Subtask Template

使用者提到，目前 RonFlow 已有：

```text
project subtask template
```

這是目前每一個 task 可以檢核完成項目的模板。

設計目的：

```text
為了應付大量任務會有一樣的步驟要走，
因此設計了 task checklist template。
```

本次訪談提出：

```text
Ready checklist 可能要設計成可以套用 project subtask template。
```

不過這部分目前仍需後續釐清。

可能方向：

```text
1. Ready checklist 取代原本 subtask list。
2. Ready checklist 與既有 project subtask template 整合。
3. 完成條件可以由 template 產生。
4. 每個 task 仍可自訂 checklist。
```

這是後續設計的重要議題。

---

## 六、父任務 Detail Modal 設計

### 1. 父任務以 Child Task List 取代 Ready checklist

使用者確認：

```text
父任務就是把 ready checklist 替換成 child task list。
```

也就是：

```text
葉任務 detail modal：顯示 Ready checklist。
父任務 detail modal：顯示 child task list。
```

### 2. 父任務 Detail Modal 中可開啟子任務

使用者希望：

```text
在父任務的 detail modal 裡面，
可以點擊 child task item，
開啟 child task 的 detail modal 進行編輯。
```

因此，父任務 detail modal 應支援：

```text
1. 顯示 child task list。
2. 點擊 child task 開啟該子任務 detail modal。
3. 可繼續編輯子任務。
```

### 3. 子任務仍需要在主畫面一目瞭然

使用者也強調：

```text
希望子任務也能在主畫面上一目瞭然。
```

因此，不能只在父任務 modal 中顯示 child task list。

主畫面仍需要某種任務樹呈現方式，例如：

```text
1. 樹狀縮排
2. 手風琴
3. 展開 / 收合
4. 並排區域中的任務樹
```

這是 UI 設計的主要挑戰之一。

---

## 七、葉任務轉父任務時的資料處理

當一個葉任務已填過 Ready checklist，後來使用者建立子任務，使其轉為父任務時：

使用者選擇：

```text
不清空 Ready checklist 資料。
Ready 狀態失效。
原本填過的資料不再顯示為主要欄位。
資料仍保留，以支援使用者反悔的情境。
```

規則可整理為：

```text
1. 葉任務新增子任務前，系統提示使用者確認。
2. 確認後，該任務轉為父任務。
3. Ready 狀態失效。
4. Ready checklist 不再顯示於主要區域。
5. 原 Ready checklist 資料保留。
6. 若使用者日後刪除所有子任務或反悔，仍可能恢復原資料。
```

---

## 八、任務樹操作

### 1. MVP 需要新增的任務樹操作

使用者表示，既有功能不重複描述，只說需要新增的功能。

新增功能為：

```text
建立子任務
```

因此，Hatchery MVP 最核心的新操作是：

```text
在任務 detail modal 中建立 child task。
```

### 2. 子任務是否有排序意義

使用者說明，目前 subtask 是有排序意義的，因為可能導入 TDD 開發流程，例如：

```text
先寫紅燈測試
再實作
再重構
```

但如果改成 child task，使用者希望：

```text
child task 不要有排序意義。
每個子任務都可以獨立完成。
```

### 3. 子任務先後依賴問題

如果真的有先後順序問題，使用者傾向透過額外機制處理，例如：

```text
某某任務完成以前，這個任務不能被進行。
```

使用者提到可能叫做：

```text
lock
```

因此後續可能新增：

```text
任務依賴 / 任務鎖定機制
```

但這不是本次 MVP 的重點。

### 4. MVP 不做拖拉改層級

使用者對「拖拉改層級」選擇：

```text
MVP 先不要。
```

因此第一版不支援：

```text
將 A 任務拖到 B 任務底下
```

如果後續需要重組任務樹，可再設計搬移功能。

### 5. 刪除父任務時，子任務一併刪除

使用者選擇：

```text
刪除父任務時，一起刪除所有子任務。
```

因此 MVP 規則為：

```text
刪除父任務 = cascade delete child tasks。
```

後續若要避免誤刪，可再補：

```text
刪除確認 modal
顯示將一併刪除的子任務數量
```

---

## 九、Ready checklist 操作規則

### 1. Ready checklist 欄位

本次訪談後，MVP Ready checklist 改為：

```text
1. 完成條件
2. 預估耗時
```

其中：

```text
完成條件可以包含產出物。
```

### 2. 填寫即可，不需要 checkbox

原本曾討論「填寫後再 checked 起來」。

本次訪談調整為：

```text
填寫即可。
```

也就是：

```text
完成條件有內容
預估耗時有內容
```

即可視為 Ready checklist 滿足。

### 3. 未填完整不可送進 Flow

使用者選擇：

```text
如果 Ready checklist 沒有填完整，不允許送進 Flow。
```

因此：

```text
完成條件未填 → 不可送進 Flow。
預估耗時未填 → 不可送進 Flow。
```

系統可：

```text
1. 禁用「送進 Flow」按鈕。
2. 或點擊時提示缺少欄位。
```

具體 UI 待後續設計。

### 4. 送進 Flow 的初始狀態

使用者選擇：

```text
一律送進 Todo，或依既有 RonFlow 機制決定。
```

MVP 暫定：

```text
Ready 葉任務送進 Flow 時，一律進入 Todo。
```

若既有 RonFlow 已有固定流程，則遵循既有機制。

---

## 十、Flow 與任務樹銜接

### 1. 任務進入 Flow 後，任務樹上的顯示

使用者表示：

```text
就依照目前既有顯示即可。
```

因此 MVP 不需額外設計太多任務樹標記。

可先沿用既有任務顯示資訊，例如：

```text
任務名稱
目前 Flow 狀態
既有任務顯示欄位
```

### 2. 已進 Flow 的任務編輯位置

使用者選擇：

```text
已進 Flow 的任務，不在任務樹中編輯，要去 Flow 編輯。
```

因此規則為：

```text
未進 Flow：在任務樹 / Hatchery 中編輯。
已進 Flow：在 Flow 中編輯。
```

這也符合第二次訪談中：

```text
Hatchery 階段只看成熟度，
進 Flow 後只看 Flow 狀態。
```

### 3. 已進 Flow 的任務仍可新增子任務，但會自動退出 Flow

使用者選擇：

```text
已進 Flow 的任務，若新增子任務，新增後自動退出 Flow。
```

規則如下：

```text
1. 已進 Flow 的葉任務可以新增子任務。
2. 新增子任務後，該任務轉為父任務。
3. Ready 狀態失效。
4. 該任務退出 Flow。
5. 該任務回到 Hatchery / 任務樹中。
```

此行為代表：

```text
如果一個已進 Flow 的任務後來發現仍需拆解，
它就不再是可執行葉任務，
因此應退出 Flow。
```

是否需要提示使用者，後續 UI 可再定義。

### 4. Flow 完成後，同步回任務樹

使用者同意：

```text
Flow 中任務 Done
↓
任務樹中的同一個葉任務也顯示完成
↓
如果父任務底下所有子任務完成，父任務自動完成
```

因此同步規則為：

```text
同一筆任務在 Flow 完成後，
任務樹上的該任務也視為完成。
```

---

## 十一、父任務進度與完成

### 1. 拆解完成一律手動

使用者選擇：

```text
拆解完成不靠系統自動判斷，一律手動。
```

因此：

```text
父任務是否拆解完成，由使用者操作標記。
```

即使所有葉任務都 Ready，也不自動判斷父任務拆解完成。

### 2. 拆解完成後仍可新增子任務

使用者選擇：

```text
MVP 先允許，之後再補紀錄。
```

因此：

```text
父任務已標記拆解完成後，仍可新增子任務。
新增後是否自動回到拆解中，MVP 可先允許簡化處理。
```

建議後續再補：

```text
狀態變更紀錄
拆解完成取消紀錄
新增子任務提示
```

### 3. 父任務進度顯示

使用者選擇：

```text
子任務完成數：3 / 10
```

因此父任務進度 MVP 顯示為：

```text
完成子任務數 / 全部子任務數
```

例如：

```text
3 / 10
```

此處需後續確認：

```text
統計 direct children，還是所有 descendant？
```

MVP 可先暫定統計直接子任務。

### 4. 父任務自動完成不需要提示

使用者選擇：

```text
不需要提示，自動顯示完成即可。
```

但使用者也補充：

```text
之後可以考慮做個特效，補足情緒價值。
```

因此 MVP：

```text
父任務自動完成時不跳通知。
```

後續可考慮：

```text
完成動畫
成就感提示
任務完成特效
```

---

## 十二、MVP 畫面範圍

### 1. MVP 需要的主要畫面

使用者確認目前主要需要：

```text
任務樹頁面
```

考量既有系統已有：

```text
Flow 看板
Task Detail Modal
```

因此 MVP 實際上是：

```text
1. 新增 / 調整 Project 中的任務樹頁面
2. 擴充既有 Task Detail Modal
3. 沿用既有 Flow 看板
```

### 2. Dashboard / 統計

使用者選擇：

```text
任務樹上只顯示最小進度，例如 3 / 10。
```

因此 MVP 不做獨立 Dashboard / 報表。

只做最小進度顯示：

```text
父任務完成數 / 子任務總數
```

### 3. Markdown 匯入

使用者選擇：

```text
暫時不需要，先手動建立。
```

因此 MVP 不做：

```text
Markdown 大綱匯入
貼上自動轉任務樹
```

此功能聽起來有價值，但可放到後續版本。

---

## 十三、第三次訪談主驗收流程

使用者同意以下流程可作為第三次訪談的核心驗收案例：

```text
1. 使用者進入 Project 的任務樹頁。
2. 使用者建立根層任務：「整理 RonFlow Hatchery 需求」。
3. 系統預設它是葉任務，顯示 Ready checklist。
4. 使用者發現它太大，點擊「建立子任務」。
5. 原任務轉為父任務，Ready checklist 不再作為主要操作。
6. 使用者建立多個子任務。
7. 某些子任務繼續被拆解。
8. 某個葉任務填寫完成條件與預估耗時。
9. 系統判斷該葉任務符合 Ready 條件。
10. 系統允許該葉任務送進 Flow。
11. 任務進入 Flow Todo。
12. Flow 完成後，任務樹中的該葉任務同步完成。
13. 所有子任務完成後，父任務自動完成。
```

---

## 十四、第三次訪談後的主要結論

### 1. UI 最大挑戰

本次訪談最重要的 UI 問題是：

```text
如何讓大型任務、子孫任務與 Flow 任務在同一窗口中一目瞭然，
但又不把大型任務直接放進 Flow？
```

第一代暫定用：

```text
任務樹與 Flow 並排顯示
```

後續可考慮：

```text
手風琴
大綱式樹狀結構
多種 view
Mind map
任務樹全頁模式
```

### 2. MVP 最小功能

MVP 應包含：

```text
1. Project 中新增任務樹頁面。
2. 任務可建立 child task。
3. 新任務預設為葉任務。
4. 建立 child task 後，原任務轉為父任務。
5. 葉任務 detail modal 顯示完成條件與預估耗時。
6. 父任務 detail modal 顯示 child task list。
7. 葉任務填完整 Ready 欄位後，可送進 Flow。
8. 未填完整 Ready 欄位，不可送進 Flow。
9. Ready 任務送進 Flow 後，一律進入 Todo 或依既有 Flow 機制。
10. Flow 任務完成後，同步回任務樹。
11. 父任務顯示子任務完成進度。
12. 所有子任務完成後，父任務自動完成。
```

### 3. MVP 暫不做項目

MVP 暫不做：

```text
1. Markdown 匯入。
2. Dashboard / 報表。
3. Mind Map 視覺化。
4. 多種 view。
5. 拖拉改層級。
6. 任務類型分類。
7. 不 Ready 原因。
8. AI 自動拆解。
9. 自動估時。
10. 複雜依賴管理。
11. 任務狀態變更紀錄。
12. 完成特效。
```

---

## 十五、後續待釐清問題

第三次訪談後，仍有幾個問題需要在下一次訪談或設計階段釐清。

### 1. 任務樹與 Flow 並排的具體版面

需進一步釐清：

```text
左任務樹、右 Flow？
上任務樹、下 Flow？
任務樹是否可收合？
任務樹節點顯示多少資訊？
Flow 是否仍維持既有 Kanban 版型？
```

### 2. Ready checklist 與 project subtask template 的關係

需進一步釐清：

```text
Ready checklist 是否取代原本 subtask list？
Project subtask template 是否可以產生完成條件？
完成條件是否可以套模板？
同一個 task 是否仍能自訂完成條件？
```

### 3. 父任務進度統計口徑

需進一步釐清：

```text
父任務進度 3 / 10 是計算直接子任務？
還是計算所有 descendant 葉任務？
父任務底下如果還有父任務，進度如何遞迴計算？
```

### 4. 已進 Flow 任務新增子任務的提示

目前規則是：

```text
已進 Flow 任務新增子任務後，自動退出 Flow。
```

但畫面上是否需要提示使用者仍需設計，例如：

```text
此任務已在 Flow 中。
新增子任務後，它將退出 Flow，並回到任務樹拆解狀態。
是否繼續？
```

### 5. 刪除父任務的風險

目前規則是：

```text
刪除父任務時，一併刪除所有子任務。
```

後續應確認是否需要：

```text
刪除確認
顯示子任務數量
防止誤刪
封存機制
```

### 6. Child task 是否真的完全無順序

使用者希望 child task 本身不要有排序意義，但目前 subtask 有排序意義。

後續需釐清：

```text
child task list 是否仍需要 UI 排序？
排序是否只影響顯示，不代表執行順序？
如果有執行順序，是否改由 lock / dependency 表示？
```

---

## 十六、下一次訪談建議

第四次訪談原本主題為：

```text
驗收、MVP 切片與技術落地
```

根據目前進度，下一次訪談可聚焦：

```text
1. 把 MVP 切成開發 backlog。
2. 定義 Gherkin acceptance criteria。
3. 明確列出使用者操作情境。
4. 補齊邊界條件。
5. 決定現有 task / subtask / template 如何遷移或相容。
6. 決定任務樹頁面與既有 Flow 的最小整合方式。
```

建議下一輪重點案例仍使用：

```text
RonFlow 需求說明書：抽象任務分解與任務成形管理
```

作為主驗收案例。
