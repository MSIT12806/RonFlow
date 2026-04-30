# Git Commit

Git commit message 是團隊對變更意圖的最小溝通單位。它不只是替版本留下痕跡，更影響後續 review、追 bug、整理 changelog 與回溯決策的成本。

RonFlow 目前建議採用接近 Conventional Commits 的格式，但不強迫全部英文。重點不是追求教條，而是讓每一筆 commit 能快速回答三件事：改了什麼、為什麼改、影響哪個區域。

建議格式如下：

```text
<type>(<scope>): <subject>
```

例如：

```text
feat(frontend): 建立專案首頁初版
fix(frontend): 修正看板欄位拖曳後狀態未更新
docs(devlog): 補上 GitHub Project 建立紀錄
test(frontend): 補首頁渲染驗收測試
refactor(domain): 拆分 task workflow 狀態轉換邏輯
```

## type 建議

- `feat`：新增使用者可感知的新功能。
- `fix`：修正 bug 或錯誤行為。
- `docs`：只改文件，不改產品行為。
- `refactor`：重構既有程式，理論上不改外部行為。
- `test`：新增或調整測試。
- `chore`：雜項維護，例如工具設定、套件版本、小型清理。
- `build`：影響建置流程、套件安裝、打包設定。
- `ci`：影響 CI/CD 流程。
- `perf`：以效能改善為主的修改。
- `revert`：回滾既有 commit。

如果一筆變更同時包含多件事情，優先拆成多個 commit，而不是發明新的 type 來容納混雜意圖。

## scope 建議

scope 用來指出變更主要落在哪一層或哪個模組。RonFlow 目前可優先使用以下範圍：

- `frontend`：Vue、Vite、畫面互動、前端測試。
- `docs`：根目錄 README、開發說明、非 tech-base 文件。
- `devlog`：開發歷程紀錄。
- `tech-base`：技術名詞、規範文件。
- `domain`：領域模型與業務規則。
- `application`：應用層協調邏輯。
- `infrastructure`：資料存取、外部系統整合。
- `api`：API 契約、控制器、端點。
- `workflow`：流程、狀態機、任務流轉。
- `playwright`：E2E 測試與瀏覽器驗證。

若 commit 很小，且 scope 反而讓訊息更模糊，可以省略 scope：

```text
docs: 修正文案中的錯字
chore: 更新本機開發指令
```

## subject 寫法

subject 建議遵守以下原則：

- 用一句話描述結果，不要寫流水帳。
- 聚焦「這個 commit 完成了什麼」，不要只描述操作手法。
- 優先寫可觀察結果，而不是「調整一下」、「修改一些東西」這類模糊語句。
- 盡量控制在單行可讀範圍內。

較佳寫法：

```text
fix(frontend): 修正首頁標題與實際產品名稱不一致
docs(tech-base): 新增 Git commit 撰寫規範
test(playwright): 驗證首頁可顯示 RonFlow 標題
```

較差寫法：

```text
fix: 修一下 bug
feat: update
docs: 修改 README 和一些文件還有補充測試說明
```

## body 與 footer

當 commit 需要補充脈絡時，再加上 body。body 適合說明：

- 為什麼要改，而不是把 diff 再講一次。
- 有沒有替代方案被放棄。
- 是否有相容性或資料遷移風險。

例如：

```text
feat(workflow): 新增任務狀態分類欄位

將顯示名稱與狀態語意拆開，避免不同團隊自訂名稱後，
系統無法判斷該狀態屬於 backlog、active 或 done 類別。
```

若有 breaking change、issue 或工作項目編號，可放在 footer：

```text
BREAKING CHANGE: workflow state 改用 category 判斷
Refs: #18
```

## 實務規範

- 一個 commit 盡量只表達一個清楚意圖。
- 功能、重構、格式化若彼此無關，應拆開提交。
- 測試若是功能完成條件的一部分，可以和功能一起提交；若是後補驗證，也可獨立成 `test`。
- 文件若只是描述某個功能變更，可以跟功能一起；若是在整理知識或規範，建議獨立成 `docs`。
- 避免長期保留 `wip`、`tmp`、`test test` 之類訊息進入主分支歷史。

## 建議使用方式

如果要從今天開始收斂習慣，可以先只守三條：

1. 每筆 commit 都寫 `type`。
2. 主旨寫結果，不寫模糊動作。
3. 一筆 commit 只放一個主要意圖。

等習慣穩定後，再逐步補上 scope、body 與 footer。