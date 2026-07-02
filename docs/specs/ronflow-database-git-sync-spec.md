# RonFlow Database Git Sync Spec

## 1. Spec Intent

本文件描述 RonFlow 的 SQLite-to-Git persistence sync 行為。

這不是 domain rule，也不是 human core flow 的一部分，而是 infrastructure / deployment / persistence 的 companion spec。
目標是讓單一使用者能在多個裝置上使用同一份 RonFlow 資料，並透過 GitHub 上的資料庫 repository 維持資料版本一致。

本文件的同步模型刻意保持簡單：

1. 單一人類使用者
2. 單一寫入者
3. 每次資料異動後立即同步
4. 透過 DbMerger supporting domain 做 SQLite snapshot 語意合併
5. 不嘗試用 Git 當作多寫入者資料庫

## 2. Scope

### 2.1 Included

```text
1. RonFlow 啟動時先建立既有 runtime database snapshot，再 pull database repository，透過 DbMerger 合併，最後 commit + push merged snapshot
2. Runtime SQLite DB 存放於 App_Data/ronflow.db
3. 以 SQLite snapshot 複製資料到 runtime DB
4. 任何成功的資料異動都會觸發 snapshot + commit + push
5. Git sync 只屬於 infrastructure concern
6. 同步失敗不應回滾本地已成功寫入的資料
```

### 2.2 Excluded

```text
1. 多寫入者協作
2. DbMerger recipe 未定義的資料語意 conflict merge
3. 離線雙向同步
4. 跨 repo schema migration orchestration
5. Git repository 之外的資料庫後端
```

## 3. Canonical Data Layout

RonFlow 的資料層分成兩個角色：

```text
1. runtime database: 由 RonFlow API 實際開啟與寫入的 SQLite 檔案
2. database repository: 放在獨立 Git repository 的 SQLite 快照
```

建議的檔案結構如下：

```text
runtime
- App_Data/ronflow.db

database repo
- ronflow.db
- metadata.json
- README.md
```

如果未來需要把 RonAuth 也納入同一個獨立 repo，應另寫一份 companion spec，明確定義 identity sync 邊界。

## 4. Runtime Flow

### 4.1 Startup Pull Flow

```text
1. RonFlow 啟動
2. 如果 runtime DB 已存在，先產生一致性 snapshot
3. git pull 取得 database repository 最新內容
4. 若 runtime snapshot 與 repository snapshot 都存在，呼叫 DbMerger 產生 merged snapshot
5. 將 merged snapshot 寫回 database repository 的 ronflow.db
6. git add
7. git commit
8. git push
9. 將 merged snapshot 複製到 runtime DB 位置
10. 如果 runtime snapshot 不存在但 repo 內已有 ronflow.db，將 repository snapshot 複製到 runtime DB 位置
11. runtime DB 建立完成後，再初始化 RonFlow API 與 SQLite tables
12. 若同步失敗，應保留清楚的 error log，但不要破壞本地可啟動性，也不得先用 repository snapshot 覆蓋既有 runtime DB
```

### 4.2 Mutation Push Flow

```text
1. 使用者透過 RonFlow API 成功完成資料異動
2. 產生一致性 snapshot
3. git pull 取得 database repository 最新內容
4. 若 repository snapshot 存在，呼叫 DbMerger 合併 runtime snapshot 與 repository snapshot
5. 將 merged snapshot 或 runtime snapshot 寫回 database repository 的 ronflow.db
6. git add
7. git commit
8. git push
9. 若 pull / merge / push 失敗，保留 pending/error 狀態，讓下一次啟動或下一次異動重試
```

### 4.3 Snapshot Rule

SQLite 檔案不能直接假設在寫入中仍可安全複製，因此 sync 流程應以一致性 snapshot 為基礎，而不是直接複製正在被鎖定的 runtime DB。

建議使用 SQLite backup API 或等效的 snapshot 機制。

## 5. Behavior Rules

1. Domain 不知道 Git sync 的存在。
2. Application 只知道 mutation 已成功，不知道 GitHub repository 細節。
3. Implement layer 負責協調 persistence sync 的商業規則。
4. Infrastructure layer 負責 Git、檔案、SQLite snapshot、MQ 或輪巡等技術細節。
5. RonFlow API acceptance tests 只驗證使用者可觀察的 API 行為，不驗證底層資料庫種類或 Git CLI 行為。
6. SQLite initialization / snapshot 與 Git clone / pull / commit / push 應由 infrastructure 層自己的測試覆蓋。
7. 啟動時的 runtime snapshot / pull / DbMerger merge / commit / push / restore 是預設行為。
8. 每次資料異動後，先 snapshot，再 pull，再用 DbMerger merge，最後 commit + push 是預設行為。
9. 若 Git sync 失敗，不得把本地 mutation 視為失敗。
10. 若 DbMerger 回報 unresolved conflict，第一版採手動解決，不應覆蓋 runtime DB。

## 6. Layering Contract

### 6.1 Implement Layer

Implement layer 表達 RonFlow 對 persistence sync 的使用規則：

```text
1. 啟動時要求資料庫可用
2. 啟動時可以要求先保存既有 runtime DB，再透過 DbMerger 同步外部資料來源
3. mutation 成功後要求同步最新 snapshot
4. sync 失敗時保留錯誤狀態，但不回滾已完成的本地 mutation
5. 不暴露 Git CLI、remote URL、branch、commit message 等技術細節給 application / domain
```

### 6.2 Infrastructure Layer

Infrastructure layer 承擔具體技術能力：

```text
1. SQLite snapshot
2. Git clone / pull / add / commit / push
3. DbMerger snapshot semantic merge
4. File system copy / atomic replace
5. 未來可能的 MQ dispatch
6. 未來可能的 polling / background worker
```

### 6.3 Test Boundary

RonFlow API tests 不應斷言 Git 行為，也不應斷言資料庫實作是 SQLite。

API 層只需要驗證使用者可觀察的行為：

```text
1. API 可以正常建立與讀取 Project / Task
2. API 的 authentication / authorization 行為不變
3. persistence sync 的技術細節沒有洩漏成 API contract
```

Infrastructure tests 才需要驗證：

```text
1. SQLite store 會初始化 schema
2. SQLite snapshot 可建立一致副本
3. Git sync adapter 可 pull / commit / push
4. implement layer 可以在 startup / mutation 後呼叫 DbMerger merge port
```

## 7. Identity And Ownership Notes

RonFlow 目前的資料模型會把 `OwnerId`、member `UserId`、`KnownUsers` 等 identity 資訊存進 SQLite。

因此 database sync 的前提是：

1. 同一個人類使用者在不同裝置上，應該使用一致的 RonAuth identity
2. 若不同裝置的 identity 不一致，project ownership 與 membership 會出現語意漂移
3. 若要把 identity 也一起同步，應另有明確的 RonAuth sync 策略

## 8. Acceptance Criteria

### 8.1 Startup

```text
1. 啟動 RonFlow 後，既有 runtime DB 會先被 snapshot，再透過 DbMerger 與 database repo 對齊
2. 成功啟動後，RonFlow 仍可正常提供 Project / Task API
3. 若 database repo 暫時不可用，啟動應有明確錯誤訊息
```

### 8.2 Mutation

```text
1. Project / Task / identity 相關資料異動成功後，應觸發同步
2. 同步成功後，database repo 內的 SQLite 檔案會更新
3. 同步失敗不應阻止本地寫入成功
```

### 8.3 Deployment

```text
1. localhost IIS deployment 仍應可正常承載 RonFlow API
2. Git sync 的加入不應再引入 WebDAV / PUT 類型的 hosting conflict
3. Production code 與 spec 的責任邊界應維持清楚
```

## 9. Open Questions

1. RonAuth 是否也要納入同一個 database repo。
2. mutation 後的 push 是否要同步等待完成，還是改成背景執行。
3. 外部 push reject 時，要不要自動 retry 或直接停止並報錯。
4. snapshot 的實作要用 SQLite backup API 還是 `VACUUM INTO`。
